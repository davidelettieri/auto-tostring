using System;
using System.Collections.Generic;
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

            if (node is ClassDeclarationSyntax cds && !HasToStringMethod(cds))
            {
                semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                var action = CodeAction.Create("Generate ToString()", c => GenerateToStringAsync(context.Document, cds, c));
                context.RegisterRefactoring(action);
            }
            else if (node is StructDeclarationSyntax sds && !HasToStringMethod(sds))
            {
                semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
                var action = CodeAction.Create("Generate ToString()", c => GenerateToStringAsync(context.Document, sds, c));
                context.RegisterRefactoring(action);
            }
        }

        private bool HasToStringMethod(TypeDeclarationSyntax cds)
        {
            return cds.Members.OfType<MethodDeclarationSyntax>().Any(p => p.Identifier.ValueText == "ToString");
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
                            SyntaxFactory.Identifier("ToString"),
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

            var r = string.Join(", ", properties.Select(p => $"{{nameof({p})}}={{{p}}}"));

            var @return = SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("$\"{{" + r + "}}\""));

            return SyntaxFactory.Block(@return);
        }
    }

}
