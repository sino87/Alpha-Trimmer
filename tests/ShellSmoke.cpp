#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <iostream>
#include <vector>
#include "../src/AlphaTrimmer.Shell/LocalizedMenu.h"

int wmain(int argc, wchar_t** argv)
{
    if (argc < 3) return 2;
    if (wcscmp(MenuTitleForLocale(L"ja-JP"), L"透明な余白をトリミング") != 0 ||
        wcscmp(MenuTitleForLocale(L"en-US"), L"Trim transparent margins") != 0 ||
        wcscmp(MenuTitleForLocale(L""), L"Trim transparent margins") != 0) return 13;
    std::wcout << L"PASS: menu language selection and English fallback\n";
    std::wstring settings = std::wstring(argv[1]) + L".language-test.ini";
    if (!WritePrivateProfileStringW(L"Preferences", L"Language", L"ja", settings.c_str()) ||
        wcscmp(MenuTitleForSettings(settings, L"en-US"), L"透明な余白をトリミング") != 0) return 14;
    if (!WritePrivateProfileStringW(L"Preferences", L"Language", L"en", settings.c_str()) ||
        wcscmp(MenuTitleForSettings(settings, L"ja-JP"), L"Trim transparent margins") != 0) return 15;
    if (!WritePrivateProfileStringW(L"Preferences", L"Language", L"", settings.c_str()) ||
        wcscmp(MenuTitleForSettings(settings, L"ja-JP"), L"透明な余白をトリミング") != 0) return 16;
    DeleteFileW(settings.c_str());
    std::wcout << L"PASS: shared preferences override menu language and follow system\n";
    HRESULT result = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
    if (FAILED(result)) return 3;
    HMODULE library = LoadLibraryW(argv[1]);
    if (!library) return 4;
    auto getClass = reinterpret_cast<LPFNGETCLASSOBJECT>(GetProcAddress(library, "DllGetClassObject"));
    if (!getClass) return 5;
    CLSID id;
    CLSIDFromString(L"{045D2ABC-85B9-4841-AB1E-FA6CE07E264D}", &id);
    IClassFactory* factory = nullptr;
    result = getClass(id, IID_IClassFactory, reinterpret_cast<void**>(&factory));
    if (FAILED(result)) return 6;
    IExplorerCommand* command = nullptr;
    result = factory->CreateInstance(nullptr, IID_PPV_ARGS(&command));
    factory->Release();
    if (FAILED(result)) return 7;
    std::vector<PIDLIST_ABSOLUTE> ids;
    for (int index = 2; index < argc; index++)
    {
        PIDLIST_ABSOLUTE item;
        result = SHParseDisplayName(argv[index], nullptr, &item, 0, nullptr);
        if (FAILED(result)) return 8;
        ids.push_back(item);
    }
    IShellItemArray* selection = nullptr;
    result = SHCreateShellItemArrayFromIDLists(static_cast<UINT>(ids.size()), const_cast<PCIDLIST_ABSOLUTE*>(ids.data()), &selection);
    for (auto item : ids) CoTaskMemFree(item);
    if (FAILED(result)) return 9;
    EXPCMDSTATE state;
    result = command->GetState(selection, FALSE, &state);
    if (FAILED(result) || state != ECS_ENABLED) return 10;
    PWSTR title = nullptr;
    result = command->GetTitle(selection, &title);
    if (FAILED(result) || !title || wcscmp(title, CurrentMenuTitle()) != 0) return 11;
    std::wcout << L"PASS: command title\n";
    CoTaskMemFree(title);
    result = command->Invoke(selection, nullptr);
    selection->Release();
    command->Release();
    FreeLibrary(library);
    CoUninitialize();
    if (FAILED(result))
    {
        std::wcerr << L"Invoke failed: " << std::hex << result << L"\n";
        return 12;
    }
    return 0;
}
