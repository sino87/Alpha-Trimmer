#include <windows.h>
#include <shlobj.h>
#include <shobjidl.h>
#include <shlwapi.h>
#include <atomic>
#include <string>
#include <vector>
#include "LocalizedMenu.h"

static const CLSID CommandId = {0x045d2abc, 0x85b9, 0x4841, {0xab, 0x1e, 0xfa, 0x6c, 0xe0, 0x7e, 0x26, 0x4d}};
static HMODULE moduleHandle;
static std::atomic<long> liveObjects{0};
static std::atomic<long> serverLocks{0};

static std::wstring ModuleDirectory()
{
    std::vector<wchar_t> buffer(32768);
    DWORD count = GetModuleFileNameW(moduleHandle, buffer.data(), static_cast<DWORD>(buffer.size()));
    if (count == 0 || count >= buffer.size())
        return {};
    std::wstring path(buffer.data(), count);
    return path.substr(0, path.find_last_of(L'\\'));
}

static HRESULT GetPaths(IShellItemArray* items, std::vector<std::wstring>& paths)
{
    if (!items)
        return E_INVALIDARG;
    DWORD count = 0;
    HRESULT result = items->GetCount(&count);
    if (FAILED(result))
        return result;
    for (DWORD index = 0; index < count; index++)
    {
        IShellItem* item = nullptr;
        result = items->GetItemAt(index, &item);
        if (FAILED(result))
            return result;
        PWSTR path = nullptr;
        result = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
        item->Release();
        if (FAILED(result))
            return result;
        paths.emplace_back(path);
        CoTaskMemFree(path);
    }
    return paths.empty() ? E_INVALIDARG : S_OK;
}

static bool Supported(const std::wstring& path)
{
    PCWSTR extension = PathFindExtensionW(path.c_str());
    return _wcsicmp(extension, L".png") == 0 || _wcsicmp(extension, L".webp") == 0;
}

static HRESULT Launch(const std::vector<std::wstring>& paths)
{
    std::wstring directory = ModuleDirectory();
    if (directory.empty())
        return E_FAIL;
    std::vector<wchar_t> temporaryBuffer(32768);
    DWORD count = GetTempPathW(static_cast<DWORD>(temporaryBuffer.size()), temporaryBuffer.data());
    if (count == 0 || count >= temporaryBuffer.size())
        return HRESULT_FROM_WIN32(GetLastError());
    std::wstring temporary(temporaryBuffer.data(), count);
    temporary += L"AlphaTrimmer";
    if (!CreateDirectoryW(temporary.c_str(), nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
        return HRESULT_FROM_WIN32(GetLastError());
    temporary += L"\\Selections";
    if (!CreateDirectoryW(temporary.c_str(), nullptr) && GetLastError() != ERROR_ALREADY_EXISTS)
        return HRESULT_FROM_WIN32(GetLastError());
    GUID id;
    HRESULT result = CoCreateGuid(&id);
    if (FAILED(result))
        return result;
    wchar_t name[40];
    StringFromGUID2(id, name, 40);
    temporary += L"\\" + std::wstring(name) + L".txt";
    HANDLE file = CreateFileW(temporary.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_TEMPORARY, nullptr);
    if (file == INVALID_HANDLE_VALUE)
        return HRESULT_FROM_WIN32(GetLastError());
    std::wstring content(1, 0xfeff);
    for (const auto& path : paths)
        content += path + L"\r\n";
    size_t byteCount = content.size() * sizeof(wchar_t);
    DWORD written = 0;
    bool saved = byteCount <= MAXDWORD && WriteFile(file, content.data(), static_cast<DWORD>(byteCount), &written, nullptr) && written == byteCount;
    DWORD error = saved ? ERROR_SUCCESS : GetLastError();
    CloseHandle(file);
    if (!saved)
    {
        DeleteFileW(temporary.c_str());
        return HRESULT_FROM_WIN32(error == ERROR_SUCCESS ? ERROR_WRITE_FAULT : error);
    }
    std::wstring executable = directory + L"\\alpha_trimmer.exe";
    std::wstring command = L"\"" + executable + L"\" --selection \"" + temporary + L"\"";
    STARTUPINFOW startup{sizeof(startup)};
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, directory.c_str(), &startup, &process))
    {
        error = GetLastError();
        DeleteFileW(temporary.c_str());
        return HRESULT_FROM_WIN32(error);
    }
    CloseHandle(process.hThread);
    CloseHandle(process.hProcess);
    return S_OK;
}

class Command final : public IExplorerCommand
{
    std::atomic<ULONG> references{1};
public:
    Command() { liveObjects++; }
    ~Command() { liveObjects--; }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        if (iid == IID_IUnknown || iid == __uuidof(IExplorerCommand))
            *output = static_cast<IExplorerCommand*>(this);
        if (!*output) return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++references; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        ULONG remaining = --references;
        if (!remaining) delete this;
        return remaining;
    }
    HRESULT STDMETHODCALLTYPE GetTitle(IShellItemArray*, PWSTR* title) override
    {
        return SHStrDupW(CurrentMenuTitle(), title);
    }
    HRESULT STDMETHODCALLTYPE GetIcon(IShellItemArray*, PWSTR* icon) override
    {
        std::wstring path = ModuleDirectory() + L"\\icon.ico";
        return SHStrDupW(path.c_str(), icon);
    }
    HRESULT STDMETHODCALLTYPE GetToolTip(IShellItemArray*, PWSTR* tooltip) override
    {
        *tooltip = nullptr;
        return E_NOTIMPL;
    }
    HRESULT STDMETHODCALLTYPE GetCanonicalName(GUID* guid) override { *guid = CommandId; return S_OK; }
    HRESULT STDMETHODCALLTYPE GetState(IShellItemArray* items, BOOL, EXPCMDSTATE* state) override
    {
        *state = ECS_HIDDEN;
        std::vector<std::wstring> paths;
        if (SUCCEEDED(GetPaths(items, paths)))
        {
            bool allSupported = true;
            for (const auto& path : paths)
                allSupported = allSupported && Supported(path);
            if (allSupported) *state = ECS_ENABLED;
        }
        return S_OK;
    }
    HRESULT STDMETHODCALLTYPE Invoke(IShellItemArray* items, IBindCtx*) override
    {
        std::vector<std::wstring> paths;
        HRESULT result = GetPaths(items, paths);
        return FAILED(result) ? result : Launch(paths);
    }
    HRESULT STDMETHODCALLTYPE GetFlags(EXPCMDFLAGS* flags) override { *flags = ECF_DEFAULT; return S_OK; }
    HRESULT STDMETHODCALLTYPE EnumSubCommands(IEnumExplorerCommand** commands) override { *commands = nullptr; return E_NOTIMPL; }
};

class Factory final : public IClassFactory
{
    std::atomic<ULONG> references{1};
public:
    Factory() { liveObjects++; }
    ~Factory() { liveObjects--; }
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID iid, void** output) override
    {
        if (!output) return E_POINTER;
        *output = nullptr;
        if (iid == IID_IUnknown || iid == IID_IClassFactory) *output = static_cast<IClassFactory*>(this);
        if (!*output) return E_NOINTERFACE;
        AddRef();
        return S_OK;
    }
    ULONG STDMETHODCALLTYPE AddRef() override { return ++references; }
    ULONG STDMETHODCALLTYPE Release() override
    {
        ULONG remaining = --references;
        if (!remaining) delete this;
        return remaining;
    }
    HRESULT STDMETHODCALLTYPE CreateInstance(IUnknown* outer, REFIID iid, void** output) override
    {
        if (outer) return CLASS_E_NOAGGREGATION;
        auto command = new Command();
        HRESULT result = command->QueryInterface(iid, output);
        command->Release();
        return result;
    }
    HRESULT STDMETHODCALLTYPE LockServer(BOOL lock) override { if (lock) serverLocks++; else serverLocks--; return S_OK; }
};

STDAPI DllGetClassObject(REFCLSID id, REFIID iid, void** output)
{
    if (id != CommandId) return CLASS_E_CLASSNOTAVAILABLE;
    auto factory = new Factory();
    HRESULT result = factory->QueryInterface(iid, output);
    factory->Release();
    return result;
}

STDAPI DllCanUnloadNow()
{
    return liveObjects == 0 && serverLocks == 0 ? S_OK : S_FALSE;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        moduleHandle = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}
