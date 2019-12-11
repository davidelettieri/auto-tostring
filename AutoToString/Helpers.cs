using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AutoToString
{
    public static class Helpers
    {
        public static IEnumerable<PropertyToPrint> FindAllProperties(INamedTypeSymbol symbol)
        {
            var props = symbol.GetMembers().Where(p => p.Kind == SymbolKind.Property && p.DeclaredAccessibility == Accessibility.Public).OfType<IPropertySymbol>();
            foreach (var item in props)
            {
                yield return new PropertyToPrint(item.Name, item.Type.IsValueType);
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
