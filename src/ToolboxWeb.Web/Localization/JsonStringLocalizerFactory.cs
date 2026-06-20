using Microsoft.Extensions.Localization;

namespace ToolboxWeb.Web.Localization;

public class JsonStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly JsonLocalizationStore _store;

    public JsonStringLocalizerFactory(JsonLocalizationStore store)
    {
        _store = store;
    }

    public IStringLocalizer Create(Type resourceSource)
    {
        return new JsonStringLocalizer(_store);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        return new JsonStringLocalizer(_store);
    }
}
