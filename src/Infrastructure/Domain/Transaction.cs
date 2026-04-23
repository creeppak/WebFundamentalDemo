namespace Infrastructure.Domain;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public TransactionType TransactionType { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
}