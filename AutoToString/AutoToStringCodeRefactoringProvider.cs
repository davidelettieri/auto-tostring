using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AutoToString
{
    [ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AutoToStringCodeRefactoringProvider)), Shared]
    internal class AutoToStringCodeRefactoringProvider : CodeRefactoringProvider
    {
        private SemanticModel semanticModel;
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var node = root.FindNode(context.Span);

            if (node is ClassDeclarationSyntax cds)
            {
                if (TryGetToStringMethod(cds, out var toStringMethod))
                {
                    await RegisterReplaceToString(context, cds, toStringMethod);
                }
                else
                {
                    await RegisterGenerateToString(context, cds);
                }
            }
            else if (node is StructDeclarationSyntax sds)
            {
                if (TryGetToStringMethod(sds, out var toStringMethod))
                {
                    await RegisterReplaceToString(context, sds, toStringMethod);
                }
                else
                {
                    await RegisterGenerateToString(context, sds);
                }
            }
        }

        private async Task RegisterReplaceToString(CodeRefactoringContext context, TypeDeclarationSyntax tds, MethodDeclarationSyntax toStringMethod)
        {
            semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var action = CodeAction.Create(Strings.ReplaceToString, c => ReplaceToStringAsync(context.Document, tds, toStringMethod, c));
            context.RegisterRefactoring(action);
        }

        private async Task RegisterGenerateToString(CodeRefactoringContext context, TypeDeclarationSyntax tds)
        {
            semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var action = CodeAction.Create(Strings.GenerateToString, c => GenerateToStringAsync(context.Document, tds, c));
            context.RegisterRefactoring(action);
        }

        private bool TryGetToStringMethod(TypeDeclarationSyntax cds, out MethodDeclarationSyntax toStringMethod)
        {
            toStringMethod = cds.Members
                                .OfType<MethodDeclarationSyntax>()
                                .FirstOrDefault(p => p.Identifier.ValueText == "ToString" &&
                                                     p.ParameterList.Parameters.Count == 0 &&
                                                     p.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                                                     p.Modifiers.Any(SyntaxKind.OverrideKeyword));

            return toStringMethod != null;
        }

        private async Task<Document> ReplaceToStringAsync(Document document, TypeDeclarationSyntax typeDec, MethodDeclarationSyntax oldToString, CancellationToken cancellationToken)
        {
            var newClassDec = typeDec.ReplaceNode(oldToString, GetToStringDeclarationSyntax(typeDec));

            var sr = await document.GetSyntaxRootAsync(cancellationToken);

            var nsr = sr.ReplaceNode(typeDec, newClassDec);

            return document.WithSyntaxRoot(nsr);
        }

        private async Task<Document> GenerateToStringAsync(Document document, TypeDeclarationSyntax structDec, CancellationToken cancellationToken)
        {
            var newClassDec = structDec.AddMembers(GetToStringDeclarationSyntax(structDec));

            var sr = await document.GetSyntaxRootAsync(cancellationToken);

            var nsr = sr.ReplaceNode(structDec, newClassDec);

            return document.WithSyntaxRoot(nsr);
        }

        private MethodDeclarationSyntax GetToStringDeclarationSyntax(TypeDeclarationSyntax typeDeclarationSyntax)
        {
            return SyntaxFactory.MethodDeclaration(
                            SyntaxFactory.List<AttributeListSyntax>(),
                            SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverrideKeyword)),
                            SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.StringKeyword)),
                            null,
                            SyntaxFactory.Identifier(Strings.ToStringMethod),
                            null,
                            SyntaxFactory.ParameterList(),
                            SyntaxFactory.List<TypeParameterConstraintClauseSyntax>(),
                            GetToStringBody(typeDeclarationSyntax),
                            null);
        }

        private BlockSyntax GetToStringBody(TypeDeclarationSyntax classDeclarationSyntax)
        {
            var sm = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);

            var properties = Helpers.FindAllProperties(sm);

            var r = string.Join(", ", properties.Select(p => p.GetPrintedValueForCSharp()));

            var @return = SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("$\"{{" + r + "}}\""));

            return SyntaxFactory.Block(@return);
        }
    }
}
