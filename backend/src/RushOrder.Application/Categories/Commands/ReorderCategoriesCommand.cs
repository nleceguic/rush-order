using FluentValidation;
using MediatR;
using RushOrder.Application.Common.Exceptions;
using RushOrder.Application.Common.Interfaces;
using RushOrder.Domain.Entities;

namespace RushOrder.Application.Categories.Commands;

public record CategorySortItem(Guid CategoryId, int SortOrder);

public record ReorderCategoriesCommand(IReadOnlyList<CategorySortItem> Items) : ICommand<Unit>;

public sealed class ReorderCategoriesCommandValidator : AbstractValidator<ReorderCategoriesCommand>
{
    public ReorderCategoriesCommandValidator()
    {
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.CategoryId).NotEmpty();
            item.RuleFor(i => i.SortOrder).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class ReorderCategoriesCommandHandler : IRequestHandler<ReorderCategoriesCommand, Unit>
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReorderCategoriesCommandHandler(ICategoryRepository categoryRepository, IUnitOfWork unitOfWork)
    {
        _categoryRepository = categoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Unit> Handle(ReorderCategoriesCommand request, CancellationToken cancellationToken)
    {
        foreach (var item in request.Items)
        {
            var category = await _categoryRepository.GetByIdAsync(item.CategoryId, cancellationToken)
                ?? throw new NotFoundException(nameof(Category), item.CategoryId);
            category.SetSortOrder(item.SortOrder);
            await _categoryRepository.UpdateAsync(category, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
