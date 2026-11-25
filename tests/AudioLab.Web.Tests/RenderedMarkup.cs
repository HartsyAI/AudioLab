// BL0006 asks code not to use the render tree, which is right for an app. This is the one place that has to read what a component
// wrote, and a renderer of our own over the framework's public Renderer is smaller than a test library for the sake of one check.
#pragma warning disable BL0006
using AudioLab.Web.Interop;
using AudioLab.Web.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Web.Tests;

/// <summary>Renders one component, with its children, and keeps what it wrote: every attribute with its value, and every text.</summary>
internal sealed class RenderedMarkup : Renderer
{
    private readonly List<(string Name, string Value)> _attributes = [];

    private readonly List<string> _texts = [];

    private readonly List<string> _markup = [];

    private readonly Dictionary<int, Type> _types = [];

    private readonly Dictionary<string, ulong> _handlers = [];

    private readonly Dictionary<(string? Element, string Name), ulong> _handlersOfElement = [];

    private ServiceProvider? _provider;

    private object? _component;

    private int _root;

    private RenderedMarkup(IServiceProvider services)
        : base(services, NullLoggerFactory.Instance)
    {
    }

    /// <inheritdoc />
    public override Dispatcher Dispatcher { get; } = Dispatcher.CreateDefault();

    /// <summary>Every attribute that was written, such as <c>style</c> with its value.</summary>
    public IReadOnlyList<(string Name, string Value)> Attributes => _attributes;

    /// <summary>The values of the attributes with the given name.</summary>
    /// <param name="name">The attribute's name.</param>
    /// <returns>The values, in the order they were written.</returns>
    public string[] Values(string name) => [.. _attributes.Where(attribute => attribute.Name == name).Select(attribute => attribute.Value)];

    /// <summary>Every piece of static markup that was written, which the compiler writes in one frame, such as a span with a literal.</summary>
    public IReadOnlyList<string> Markup => _markup;

    /// <summary>Every text that was written.</summary>
    public IReadOnlyList<string> Texts => _texts;

    /// <summary>How many batches of output were written: one for each render of a component that changed what it shows.</summary>
    public int Batches { get; private set; }

    /// <summary>How many components of a type were ever made: a component that is kept is made once, one that is replaced is made again.</summary>
    public int Made(Type type) => _types.Values.Count(candidate => candidate == type);

    /// <summary>The types of the components drawn in the latest batch, one for each draw: what a change cost.</summary>
    public IReadOnlyList<Type> Drawn { get; private set; } = [];

    /// <summary>Renders <typeparamref name="T"/> with the given parameters.</summary>
    /// <typeparam name="T">The component.</typeparam>
    /// <param name="parameters">The parameters by name.</param>
    /// <param name="services">Services the component and its children inject, if any.</param>
    /// <returns>What was written.</returns>
    public static async Task<RenderedMarkup> OfAsync<T>(IDictionary<string, object?> parameters, Action<IServiceCollection>? services = null)
        where T : IComponent
    {
        RenderedMarkup renderer = await StartAsync<T>(parameters, services);
        await renderer.DisposeServicesAsync();
        return renderer;
    }

    /// <summary>Renders <typeparamref name="T"/> and keeps it alive, so that it can be given new parameters and events; dispose when done.</summary>
    /// <typeparam name="T">The component.</typeparam>
    /// <param name="parameters">The parameters by name.</param>
    /// <param name="services">Services the component and its children inject, if any.</param>
    /// <returns>What was written, which goes on being kept.</returns>
    public static async Task<RenderedMarkup> StartAsync<T>(IDictionary<string, object?> parameters, Action<IServiceCollection>? services = null)
        where T : IComponent
    {
        ServiceCollection collection = new();
        services?.Invoke(collection);
        // The live meters and the playhead read the playback service, which here has an engine that makes no sound.
        collection.TryAddSingleton<IAudioHost, FakeAudioHost>();
        collection.TryAddSingleton<IPointerTracker, NoPointerTracker>();
        collection.TryAddSingleton(new HttpClient { BaseAddress = new Uri("http://example.test/") });
        collection.TryAddSingleton(TimeProvider.System);
        collection.TryAddSingleton<PlaybackService>();
        ServiceProvider provider = collection.BuildServiceProvider();
        RenderedMarkup renderer = new(provider) { _provider = provider };
        T component = (T)renderer.InstantiateComponent(typeof(T));
        renderer._component = component;
        renderer._root = renderer.AssignRootComponentId(component);
        renderer._types[renderer._root] = typeof(T);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootComponentAsync(renderer._root, ParameterView.FromDictionary(parameters)));
        return renderer;
    }

    /// <summary>Gives the root new parameters, as its parent would after a change; <see cref="Drawn"/> then says what was drawn again.</summary>
    public Task RenderAgainAsync(IDictionary<string, object?> parameters)
    {
        return Dispatcher.InvokeAsync(() => RenderRootComponentAsync(_root, ParameterView.FromDictionary(parameters)));
    }

    /// <summary>Raises an event on the element that has a handler for <paramref name="name"/>, such as <c>onpointerdown</c>.</summary>
    public Task RaiseAsync(string name, EventArgs args)
    {
        return Dispatcher.InvokeAsync(() => DispatchEventAsync(_handlers[name], new EventFieldInfo(), args));
    }

    /// <summary>Raises an event on the element with the given id, such as <c>onkeydown</c> on a step's button.</summary>
    /// <param name="elementId">The id the element was written with.</param>
    /// <param name="name">The event, such as <c>onkeydown</c>.</param>
    /// <param name="args">The event's arguments.</param>
    public Task RaiseOnAsync(string elementId, string name, EventArgs args)
    {
        return Dispatcher.InvokeAsync(() => DispatchEventAsync(_handlersOfElement[(elementId, name)], new EventFieldInfo(), args));
    }

    /// <summary>The rendered component itself, for work that a test calls on it directly: a handler's id is only sent again when the
    /// handler changes, so a test that needs a particular element's handler calls the method the handler calls.</summary>
    /// <typeparam name="T">The component.</typeparam>
    public T Instance<T>() where T : class => (T)_component!;

    /// <summary>Runs work on the renderer's own context, as a callback from the browser does.</summary>
    public Task RunAsync(Action work) => Dispatcher.InvokeAsync(work);

    /// <summary>Runs work that finishes later on the renderer's own context, and returns when it has finished.</summary>
    public Task RunAsync(Func<Task> work) => Dispatcher.InvokeAsync(work);

    /// <summary>Takes the components down, as the page does when it leaves, so that what they do on the way out can be seen.</summary>
    public void TakeDown() => Dispose();

    /// <summary>Lets go of the services of a render kept by <see cref="StartAsync{T}"/>.</summary>
    public async Task DisposeServicesAsync()
    {
        if (_provider is { } provider)
        {
            _provider = null;
            await provider.DisposeAsync();
        }
    }

    /// <inheritdoc />
    protected override void HandleException(Exception exception) => System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(exception);

    /// <inheritdoc />
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        Batches++;
        ArrayRange<RenderTreeFrame> frames = renderBatch.ReferenceFrames;
        string? element = null;
        for (int i = 0; i < frames.Count; i++)
        {
            RenderTreeFrame frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Component)
            {
                _types[frame.ComponentId] = frame.ComponentType;
            }
            else if (frame.FrameType == RenderTreeFrameType.Element)
            {
                element = null;
            }
            else if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeName == "id")
            {
                element = frame.AttributeValue?.ToString();
            }
            else if (frame.FrameType == RenderTreeFrameType.Attribute && frame.AttributeEventHandlerId != 0)
            {
                _handlers[frame.AttributeName] = frame.AttributeEventHandlerId;
                _handlersOfElement[(element, frame.AttributeName)] = frame.AttributeEventHandlerId;
            }
            if (frame.FrameType == RenderTreeFrameType.Attribute)
            {
                _attributes.Add((frame.AttributeName, frame.AttributeValue?.ToString() ?? string.Empty));
            }
            else if (frame.FrameType == RenderTreeFrameType.Text)
            {
                _texts.Add(frame.TextContent);
            }
            else if (frame.FrameType == RenderTreeFrameType.Markup)
            {
                _markup.Add(frame.MarkupContent);
            }
        }
        List<Type> drawn = [];
        ArrayRange<RenderTreeDiff> updated = renderBatch.UpdatedComponents;
        for (int i = 0; i < updated.Count; i++)
        {
            drawn.Add(_types.GetValueOrDefault(updated.Array[i].ComponentId) ?? typeof(void));
        }
        Drawn = drawn;
        return Task.CompletedTask;
    }
}
