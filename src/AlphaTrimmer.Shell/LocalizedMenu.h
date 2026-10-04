#pragma once
#include <string>

struct ShellTranslation
{
    const wchar_t* culture;
    const wchar_t* title;
};

#include "../../artifacts/localization/ShellStrings.h"

inline const wchar_t* MenuTitleForLocale(std::wstring culture)
{
    while (!culture.empty())
    {
        for (const auto& translation : ShellTranslations)
            if (_wcsicmp(culture.c_str(), translation.culture) == 0)
                return translation.title;
        wchar_t parent[LOCALE_NAME_MAX_LENGTH]{};
        if (GetLocaleInfoEx(culture.c_str(), LOCALE_SPARENT, parent, LOCALE_NAME_MAX_LENGTH) > 0 && culture != parent)
        {
            culture = parent;
            continue;
        }
        size_t separator = culture.find_last_of(L'-');
        if (separator == std::wstring::npos) break;
        culture.resize(separator);
    }
    return ShellTranslations[0].title;
}

inline const wchar_t* MenuTitleForSettings(const std::wstring& settings, const wchar_t* systemCulture)
{
    wchar_t selected[LOCALE_NAME_MAX_LENGTH]{};
    GetPrivateProfileStringW(L"Preferences", L"Language", L"", selected, LOCALE_NAME_MAX_LENGTH, settings.c_str());
    for (const auto& translation : ShellTranslations)
        if (_wcsicmp(selected, translation.culture) == 0) return translation.title;
    return MenuTitleForLocale(systemCulture);
}

inline const wchar_t* CurrentMenuTitle()
{
    wchar_t culture[LOCALE_NAME_MAX_LENGTH]{};
    LCIDToLocaleName(MAKELCID(GetUserDefaultUILanguage(), SORT_DEFAULT), culture, LOCALE_NAME_MAX_LENGTH, 0);
    PWSTR localData = nullptr;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, nullptr, &localData)))
    {
        std::wstring settings = std::wstring(localData) + L"\\AlphaTrimmer\\settings.ini";
        CoTaskMemFree(localData);
        return MenuTitleForSettings(settings, culture);
    }
    return MenuTitleForLocale(culture);
}
