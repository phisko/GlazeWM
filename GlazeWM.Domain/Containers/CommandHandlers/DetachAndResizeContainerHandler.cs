using System;
using System.Linq;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Domain.Workspaces;
using GlazeWM.Infrastructure.Bussing;

namespace GlazeWM.Domain.Containers.CommandHandlers
{
  internal sealed class DetachAndResizeContainerHandler : ICommandHandler<DetachAndResizeContainerCommand>
  {
    private readonly Bus _bus;

    public DetachAndResizeContainerHandler(Bus bus)
    {
      _bus = bus;
    }

    public CommandResponse Handle(DetachAndResizeContainerCommand command)
    {
      var childToRemove = command.ChildToRemove;

      if (childToRemove is not IResizable)
        throw new Exception("Cannot resize a non-resizable container. This is a bug.");

      var lastRemovedContainer = ContainerService.GetLastFlattenedContainer(childToRemove) ?? childToRemove;
      // Get the freed up space after container is detached.
      var availableSizePercentage = (lastRemovedContainer as IResizable).SizePercentage;
      // Resize children of grandparent if `childToRemove`'s parent is also to be detached.
      var containersToResize = lastRemovedContainer.Parent.ChildrenOfType<IResizable>();

      _bus.Invoke(new DetachContainerCommand(childToRemove));

      var sizePercentageIncrement = availableSizePercentage / containersToResize.Count();

      // Adjust `SizePercentage` of the siblings of the removed container.
      foreach (var containerToResize in containersToResize)
      {
        ((IResizable)containerToResize).SizePercentage += sizePercentageIncrement;
      }

      return CommandResponse.Ok;
    }
  }
}
