namespace OrderDesk.Api.Domain;

/// <summary>The only place that decides which order status changes are legal.</summary>
public static class OrderStateMachine
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.InProgress, OrderStatus.Cancelled],
        [OrderStatus.InProgress] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
        [OrderStatus.Cancelled] = [],
    };

    public static bool CanMove(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);

    public static void Move(Order order, OrderStatus to)
    {
        if (!CanMove(order.Status, to))
        {
            throw new InvalidOrderTransitionException(order.Status, to);
        }

        order.Status = to;
    }
}

public class InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
    : Exception($"An order that is {from} cannot become {to}.")
{
    public OrderStatus From { get; } = from;
    public OrderStatus To { get; } = to;
}
