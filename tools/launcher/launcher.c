// Tiny launcher: lives at the top of the bundle next to README.txt and runs the
// real app from the .\app subfolder. Keeps the top level clean (just this exe +
// readme) while all the runtime DLLs stay tucked in .\app.
#include <windows.h>
#include <shellapi.h>
#include <string.h>

int WINAPI WinMain(HINSTANCE hInst, HINSTANCE hPrev, LPSTR lpCmd, int nShow)
{
    char dir[MAX_PATH];
    GetModuleFileNameA(NULL, dir, MAX_PATH);
    char *slash = strrchr(dir, '\\');
    if (slash) *slash = '\0';

    char appExe[MAX_PATH];
    char appDir[MAX_PATH];
    wsprintfA(appDir, "%s\\app", dir);
    wsprintfA(appExe, "%s\\app\\HDRSnap2.exe", dir);

    SHELLEXECUTEINFOA sei = { sizeof(sei) };
    sei.lpVerb = "open";
    sei.lpFile = appExe;
    sei.lpDirectory = appDir;      // so the app finds its DLLs in .\app
    sei.nShow = SW_SHOWNORMAL;
    ShellExecuteExA(&sei);
    return 0;
}
