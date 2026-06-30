using FluentValidation;
using TaskApi.Web.Models;

namespace TaskApi.Web.Validation;

public class TaskItemValidator : AbstractValidator<TaskItem>
{
    public TaskItemValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .MaximumLength(200);
    }
}
