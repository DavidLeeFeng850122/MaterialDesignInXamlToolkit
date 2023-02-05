namespace MaterialDesignColors;

[MarkupExtensionReturnType(typeof(object))]
[Localizability(LocalizationCategory.NeverLocalize)]
public class DynamicResourceExtension : System.Windows.StaticResourceExtension
{
    public DynamicResourceExtension()
    {
    }

    public DynamicResourceExtension(object resourceKey)
        : base(resourceKey)
    {
    }
}
