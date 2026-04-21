using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

public class KeyboardHook
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int VK_F1 = 0x70; // Код клавиши F1

    public event Action? OnKeyDown;
    public event Action? OnKeyUp;

    public void ClearSubscribers()
    {
        OnKeyDown = null;
        OnKeyUp = null;
    }

    private LowLevelKeyboardProc _proc;
    private IntPtr _hookID = IntPtr.Zero;

    public KeyboardHook()
    {
        _proc = HookCallback;
    }

    public void SetHook() => _hookID = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName), 0);

    public void Unhook() => UnhookWindowsHookEx(_hookID);

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            if (vkCode == VK_F1)
            {
                if (wParam == (IntPtr)WM_KEYDOWN) OnKeyDown?.Invoke();
                if (wParam == (IntPtr)WM_KEYUP) OnKeyUp?.Invoke();
            }
        }
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    // Импорт WinAPI методов
    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
}