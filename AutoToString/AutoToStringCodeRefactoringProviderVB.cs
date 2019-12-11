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

            if (node.Parent is ClassBlockSyntax cds && !HasToStringMethod(cds))
            {
                semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                var action = CodeAction.Create("Generate ToString()", c => GenerateToStringAsync(context.Document, cds, c));
                context.RegisterRefactoring(action);
            }
            else if (node.Parent is StructureBlockSyntax sds && !HasToStringMethod(sds))
            {
                semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                var action = CodeAction.Create("Generate ToString()", c => GenerateToStringAsync(context.Document, sds, c));
                context.RegisterRefactoring(action);
            }
        }

        private bool HasToStringMethod(TypeBlockSyntax cds)
        {
            return cds.Members.OfType<MethodBlockSyntax>().Any(p => p.SubOrFunctionStatement.Identifier.ValueText == "ToString");
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
                                                          SyntaxFactory.Identifier("ToString"),
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

            var r = string.Join(", ", properties.Select(p => p.GetPrintedValue()));

            return SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("$\"{{" + r + "}}\""));
        }
    }
}
