using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AutoToString
{
    [ExportCodeRefactoringProvider(LanguageNames.VisualBasic, Name = nameof(AutoToStringCodeRefactoringProviderVB)), Shared]
    internal class AutoToStringCodeRefactoringProviderVB : CodeRefactoringProvider
    {
        private SemanticModel semanticModel;
        public sealed override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var node = root.FindNode(context.Span);

            if (node.Parent is ClassBlockSyntax cds)
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
            else if (node.Parent is StructureBlockSyntax sds)
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

        private async Task RegisterReplaceToString(CodeRefactoringContext context, TypeBlockSyntax tds, MethodBlockSyntax toStringMethod)
        {
            semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var action = CodeAction.Create(Strings.ReplaceToString, c => ReplaceToStringAsync(context.Document, tds, toStringMethod, c));
            context.RegisterRefactoring(action);
        }

        private async Task RegisterGenerateToString(CodeRefactoringContext context, TypeBlockSyntax cds)
        {
            semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
            var action = CodeAction.Create(Strings.GenerateToString, c => GenerateToStringAsync(context.Document, cds, c));
            context.RegisterRefactoring(action);
        }

        private bool TryGetToStringMethod(TypeBlockSyntax cds, out MethodBlockSyntax toStringMethod)
        {
            toStringMethod = cds.Members
                                .OfType<MethodBlockSyntax>()
                                .FirstOrDefault(p => p.SubOrFunctionStatement.Identifier.ValueText == "ToString" &&
                                                     p.SubOrFunctionStatement.ParameterList.Parameters.Count == 0 &&
                                                     p.SubOrFunctionStatement.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                                                     p.SubOrFunctionStatement.Modifiers.Any(SyntaxKind.OverridesKeyword));

            return toStringMethod != null;
        }

        private async Task<Document> ReplaceToStringAsync(Document document, TypeBlockSyntax typeDec, MethodBlockSyntax oldToString, CancellationToken cancellationToken)
        {
            var newClassDec = typeDec.ReplaceNode(oldToString, GetToStringDeclarationSyntax(typeDec));

            var sr = await document.GetSyntaxRootAsync(cancellationToken);

            var nsr = sr.ReplaceNode(typeDec, newClassDec);

            return document.WithSyntaxRoot(nsr);
        }

        private async Task<Document> GenerateToStringAsync(Document document, TypeBlockSyntax typeBlockSyntax, CancellationToken cancellationToken)
        {
            var newTypeDec = typeBlockSyntax.AddMembers(GetToStringDeclarationSyntax(typeBlockSyntax));

            var sr = await document.GetSyntaxRootAsync(cancellationToken);

            var nsr = sr.ReplaceNode(typeBlockSyntax, newTypeDec);

            return document.WithSyntaxRoot(nsr);
        }

        private MethodBlockSyntax GetToStringDeclarationSyntax(TypeBlockSyntax typeDeclarationSyntax)
        {
            var subStatement = SyntaxFactory.FunctionStatement(SyntaxFactory.List<AttributeListSyntax>(),
                                                          SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.OverridesKeyword)),
                                                          SyntaxFactory.Identifier(Strings.ToStringMethod),
                                                          null,
                                                          SyntaxFactory.ParameterList(),
                                                          SyntaxFactory.SimpleAsClause(SyntaxFactory.IdentifierName("String")),
                                                          null,
                                                          null);

            var statements = SyntaxFactory.List(new[] { GetToStringBody(typeDeclarationSyntax) });
            var endSubStatement = SyntaxFactory.EndFunctionStatement();
            return SyntaxFactory.FunctionBlock(subStatement, statements, endSubStatement);
        }

        private StatementSyntax GetToStringBody(TypeBlockSyntax classDeclarationSyntax)
        {
            var sm = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);

            var properties = Helpers.FindAllProperties(sm);

            var r = string.Join(", ", properties.Select(p => p.GetPrintedValueForVB()));

            return SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("$\"{{" + r + "}}\""));
        }
    }
}
