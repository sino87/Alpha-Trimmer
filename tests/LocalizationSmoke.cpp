#include <cwchar>
#include <iostream>

struct ShellTranslation
{
    const wchar_t* culture;
    const wchar_t* title;
};

#include "ShellStrings.h"

int main()
{
    for (const auto& translation : ShellTranslations)
        if (wcscmp(translation.culture, L"fr") == 0 &&
            wcscmp(translation.title, L"Rogner l'image \"alpha\" <test> \\") == 0)
        {
            std::cout << "PASS: translated punctuation survives native compilation\n";
            return 0;
        }
    return 1;
}
