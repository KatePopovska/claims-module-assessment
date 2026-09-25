using ClaimsModule.Domain.Documents;
using FluentValidation;

namespace ClaimsModule.Application.Documents.Commands.UploadClaimDocument;

public class UploadClaimDocumentCommandValidator : AbstractValidator<UploadClaimDocumentCommand>
{
    public UploadClaimDocumentCommandValidator()
    {
        RuleFor(c => c.ClaimId).NotEmpty();

        RuleFor(c => c.FileName)
            .NotEmpty().WithMessage("A file is required.")
            .MaximumLength(255)
            .Must(fileName => DocumentPolicy.GetContentType(fileName) is not null)
            .WithMessage("File type is not allowed. Allowed types: PDF, JPEG, PNG, DOCX, XLSX, TXT, CSV.");

        RuleFor(c => c.Length)
            .GreaterThan(0).WithMessage("The file is empty.")
            .LessThanOrEqualTo(DocumentPolicy.MaxFileSizeBytes).WithMessage("The file exceeds the 50 MB limit.");

        RuleFor(c => c.DocumentType).MaximumLength(50);
        RuleFor(c => c.Notes).MaximumLength(2000);
    }
}
