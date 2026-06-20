using System.Globalization;
using Microsoft.Extensions.Localization;

namespace ToolboxWeb.Web.Localization;

public class JsonStringLocalizer : IStringLocalizer
{
    private readonly JsonLocalizationStore _store;

    public JsonStringLocalizer(JsonLocalizationStore store)
    {
        _store = store;
    }

    public LocalizedString this[string name]
    {
        get
        {
            var result = _store.GetString(CultureInfo.CurrentUICulture.Name, name);
            return new LocalizedString(name, result.Value, !result.Found, _store.SearchedLocation);
        }
    }

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var result = this[name];
            var value = string.Format(CultureInfo.CurrentCulture, result.Value, arguments);
            return new LocalizedString(name, value, result.ResourceNotFound, result.SearchedLocation);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        return _store
            .GetAllStrings(CultureInfo.CurrentUICulture.Name, includeParentCultures)
            .Select(x => new LocalizedString(x.Key, x.Value, resourceNotFound: false, searchedLocation: _store.SearchedLocation));
    }
}
