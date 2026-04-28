using Infrastructure.Domain;
using Riok.Mapperly.Abstractions;
using Shared.Portfolio;

namespace Api.Mappers;

[Mapper]
public partial class PortfolioMapper
{
    [MapperIgnoreSource(nameof(Transaction.UserId))]
    public partial TransactionDto ToTransactionDto(Transaction transaction);
}