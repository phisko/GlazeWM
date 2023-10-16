using System.Linq;
using GlazeWM.Domain.Common.Enums;
using GlazeWM.Domain.Containers.Commands;
using GlazeWM.Infrastructure.Bussing;
using GlazeWM.Infrastructure.Utils;

namespace GlazeWM.Domain.Containers.CommandHandlers
{
  internal sealed class FlattenSplitContainerHandler : ICommandHandler<FlattenSplitContainerCommand>
  {
    private readonly Bus _bus;
    private readonly ContainerService _containerService;

    public FlattenSplitContainerHandler(Bus bus, ContainerService containerService)
    {
      _bus = bus;
      _containerService = containerService;
    }

    public CommandResponse Handle(FlattenSplitContainerCommand command)
    {
      var containerToFlatten = command.ContainerToFlatten;

      // Keep references to properties of container to flatten prior to detaching.
      var originalParent = containerToFlatten.Parent;
      var originalChildren = containerToFlatten.Children.ToList();
      var originalFocusIndex = containerToFlatten.FocusIndex;
      var originalIndex = containerToFlatten.Index;
      var originalFocusOrder = containerToFlatten.ChildFocusOrder.ToList();

      foreach (var (child, index) in originalChildren.WithIndex())
      {
        // TODO: Not 100% sure what to do here. When flattening a split container, inverse
        // the tiling direction of child split containers. Atm this crashes with H[1 V[2 H[3 V[4]]]],
        // where container 2 is closed.
        if (child is SplitContainer splitContainer)
          (child as SplitContainer).TilingDirection = splitContainer.TilingDirection.Inverse();

        // Insert children of the split container at its original index in the parent. The split
        // container will automatically detach once its last child is detached.
        _bus.Invoke(new DetachContainerCommand(child));
        _bus.Invoke(new AttachContainerCommand(child, originalParent, originalIndex + index));

        (child as IResizable).SizePercentage = (containerToFlatten as IResizable).SizePercentage
          * (child as IResizable).SizePercentage;
      }

      // Correct focus order of the inserted containers.
      foreach (var child in originalChildren)
      {
        var childFocusIndex = originalFocusOrder.IndexOf(child);
        originalParent.ChildFocusOrder.ShiftToIndex(originalFocusIndex + childFocusIndex, child);
      }

      _containerService.ContainersToRedraw.Add(originalParent);

      return CommandResponse.Ok;
    }
  }
}
