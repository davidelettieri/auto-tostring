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

            var typeDecl = node as ClassDeclarationSyntax;

            if (typeDecl == null || typeDecl.Members.OfType<MethodDeclarationSyntax>().Any(p => p.Identifier.ValueText == "ToString"))
            {
                return;
            }
            semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

            var action = CodeAction.Create("Generate ToString()", c => GenerateToStringAsync(context.Document, typeDecl, c));

            context.RegisterRefactoring(action);
        }

        private async Task<Document> GenerateToStringAsync(Document document, ClassDeclarationSyntax classDec, CancellationToken cancellationToken)
        {
            var newClassDec = classDec.AddMembers(GetToStringDeclarationSyntax(classDec));

            var sr = await document.GetSyntaxRootAsync();

            var nsr = sr.ReplaceNode(classDec, newClassDec);

            return document.WithSyntaxRoot(nsr);
        }

        private MethodDeclarationSyntax GetToStringDeclarationSyntax(ClassDeclarationSyntax classDeclarationSyntax)
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
                            GetToStringBody(classDeclarationSyntax),
                            null);
        }

        private BlockSyntax GetToStringBody(ClassDeclarationSyntax classDeclarationSyntax)
        {
            var sm = semanticModel.GetDeclaredSymbol(classDeclarationSyntax);

            var properties = FindAllProperties(sm); sm.GetMembers().Where(p => p.Kind == SymbolKind.Property);

            var r = string.Join(", ", properties.Select(p => $"{{nameof({p})}}={{{p}}}"));

            var @return = SyntaxFactory.ReturnStatement(SyntaxFactory.ParseExpression("$\"{{" + r + "}}\""));

            return SyntaxFactory.Block(@return);
        }

        private IEnumerable<string> FindAllProperties(INamedTypeSymbol symbol)
        {
            var props = symbol.GetMembers().Where(p => p.Kind == SymbolKind.Property && p.DeclaredAccessibility == Accessibility.Public);

            foreach (var item in props)
            {
                yield return item.Name;
            }

            if (!string.Equals(symbol.BaseType?.Name, "object", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var item in FindAllProperties(symbol.BaseType))
                {
                    yield return item;
                }
            }
        }
    }

}
