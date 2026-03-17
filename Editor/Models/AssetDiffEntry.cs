namespace GitEditor
{
    public enum AssetDiffChangeType
    {
        PropertyChanged,
        ComponentAdded,
        ComponentRemoved,
        GameObjectAdded,
        GameObjectRemoved,
        Unrecognized
    }

    public class AssetDiffEntry
    {
        public AssetDiffChangeType ChangeType;
        public string ObjectName;
        public string ComponentName;
        public string PropertyName;
        public string OldValue;
        public string NewValue;
        public string RawLine;

        public string ToDisplayString()
        {
            switch (ChangeType)
            {
                case AssetDiffChangeType.PropertyChanged:
                    string obj = !string.IsNullOrEmpty(ObjectName) ? $"'{ObjectName}'" : "'?'";
                    string comp = !string.IsNullOrEmpty(ComponentName) ? $".'{ComponentName}'" : "";
                    string prop = !string.IsNullOrEmpty(PropertyName) ? $".'{PropertyName}'" : "";
                    return $"{obj}{comp}{prop} {OldValue} -> {NewValue}";

                case AssetDiffChangeType.ComponentAdded:
                    return $"'{ObjectName ?? "?"}' +component '{ComponentName ?? "?"}'";

                case AssetDiffChangeType.ComponentRemoved:
                    return $"'{ObjectName ?? "?"}' -component '{ComponentName ?? "?"}'";

                case AssetDiffChangeType.GameObjectAdded:
                    return $"+gameObject '{ObjectName ?? "?"}'";

                case AssetDiffChangeType.GameObjectRemoved:
                    return $"-gameObject '{ObjectName ?? "?"}'";

                case AssetDiffChangeType.Unrecognized:
                    return RawLine ?? "";

                default:
                    return RawLine ?? "";
            }
        }
    }
}
