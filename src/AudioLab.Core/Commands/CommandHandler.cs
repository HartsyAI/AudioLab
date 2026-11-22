using AudioLab.Core.Model;

namespace AudioLab.Core.Commands;

/// <summary>Applies a command to a document: pure and deterministic given the id service, and it refuses by result, never by exception.</summary>
/// <typeparam name="TCommand">The command type the handler serves.</typeparam>
/// <param name="document">The document to edit; the handler never changes it, it returns a new one.</param>
/// <param name="command">The command, whose project already matches the document.</param>
/// <param name="ids">The only source of new ids.</param>
public delegate CommandResult CommandHandler<in TCommand>(ProjectDocument document, TCommand command, IIdService ids)
    where TCommand : Command;
