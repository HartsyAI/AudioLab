// BL0006 asks code not to use the render tree, which is right for an app. This is the one place that has to read what a component
// wrote, and a renderer of our own over the framework's public Renderer is smaller than a test library for the sake of one check.
#pragma warning disable BL0006
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AudioLab.Web.Tests;

/// <summary>Renders one component, with its children, and keeps what it wrote: every attribute with its value, and every text.</summary>
internal sealed class RenderedMarkup : Renderer
{
    private readonly List<(string Name, string Value)> _attributes = [];

    private readonly List<string> _texts = [];

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

    /// <summary>Every text that was written.</summary>
    public IReadOnlyList<string> Texts => _texts;

    /// <summary>Renders <typeparamref name="T"/> with the given parameters.</summary>
    /// <typeparam name="T">The component.</typeparam>
    /// <param name="parameters">The parameters by name.</param>
    /// <param name="services">Services the component and its children inject, if any.</param>
    /// <returns>What was written.</returns>
    public static async Task<RenderedMarkup> OfAsync<T>(IDictionary<string, object?> parameters, Action<IServiceCollection>? services = null)
        where T : IComponent
    {
        ServiceCollection collection = new();
        services?.Invoke(collection);
        await using ServiceProvider provider = collection.BuildServiceProvider();
        RenderedMarkup renderer = new(provider);
        T component = (T)renderer.InstantiateComponent(typeof(T));
        int id = renderer.AssignRootComponentId(component);
        await renderer.Dispatcher.InvokeAsync(() => renderer.RenderRootComponentAsync(id, ParameterView.FromDictionary(parameters)));
        return renderer;
    }

    /// <inheritdoc />
    protected override void HandleException(Exception exception) => throw exception;

    /// <inheritdoc />
    protected override Task UpdateDisplayAsync(in RenderBatch renderBatch)
    {
        ArrayRange<RenderTreeFrame> frames = renderBatch.ReferenceFrames;
        for (int i = 0; i < frames.Count; i++)
        {
            RenderTreeFrame frame = frames.Array[i];
            if (frame.FrameType == RenderTreeFrameType.Attribute)
            {
                _attributes.Add((frame.AttributeName, frame.AttributeValue?.ToString() ?? string.Empty));
            }
            else if (frame.FrameType == RenderTreeFrameType.Text)
            {
                _texts.Add(frame.TextContent);
            }
        }
        return Task.CompletedTask;
    }
}
