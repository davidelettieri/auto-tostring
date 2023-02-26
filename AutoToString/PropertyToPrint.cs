namespace AutoToString
{
    public class PropertyToPrint
    {
        public string Name { get; }
        public bool IsValueType { get; }

        public PropertyToPrint(string name, bool isValueType)
        {
            Name = name;
            IsValueType = isValueType;
        }

        public string GetPrintedValueForCSharp()
        {
            return IsValueType ? $"{{ nameof({Name})}} = {{{Name}.ToString()}} " : $" {{nameof({Name})}} = {{{Name}}} ";
        }

        public string GetPrintedValueForVB()
        {
            return IsValueType ? $"{{NameOf({Name})}}={{{Name}.ToString()}}" : $"{{NameOf({Name})}}={{{Name}}}";
        }
    }
}
