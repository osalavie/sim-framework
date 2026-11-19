using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Sim.Admin.Wpf.Client.Services;

/// <summary>「记住我」使用 Windows DPAPI（当前用户作用域）加密保存账号密码，磁盘上不写明文。</summary>
internal static class CredentialStore
{
    private const int CryptProtectUiForbidden = 0x1;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Sim.Admin.Wpf.Client", "remembered-login.dat");

    public static (string UserName, string Password)? Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var payload = JsonSerializer.Deserialize<Payload>(Unprotect(File.ReadAllBytes(FilePath)));
            if (payload is null || string.IsNullOrWhiteSpace(payload.UserName)) return null;
            return (payload.UserName, payload.Password ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    public static void Save(string userName, string password)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var payload = JsonSerializer.SerializeToUtf8Bytes(new Payload(userName, password));
            File.WriteAllBytes(FilePath, Protect(payload));
        }
        catch
        {
            // 记住我属于尽力而为的本机体验，加密/落盘失败时不影响登录。
        }
    }

    public static void Clear()
    {
        try { File.Delete(FilePath); } catch { }
    }

    private static byte[] Protect(byte[] data) => Transform(data, protect: true);

    private static byte[] Unprotect(byte[] data) => Transform(data, protect: false);

    private static byte[] Transform(byte[] data, bool protect)
    {
        var input = new DataBlob();
        var output = new DataBlob();
        var inputPtr = Marshal.AllocHGlobal(data.Length);
        try
        {
            Marshal.Copy(data, 0, inputPtr, data.Length);
            input.cbData = data.Length;
            input.pbData = inputPtr;

            var ok = protect
                ? CryptProtectData(ref input, "Sim.Admin.Wpf.Client login", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out output)
                : CryptUnprotectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                    CryptProtectUiForbidden, out output);
            if (!ok || output.pbData == IntPtr.Zero) throw new InvalidOperationException("DPAPI 操作失败。");

            var result = new byte[output.cbData];
            Marshal.Copy(output.pbData, result, 0, output.cbData);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(inputPtr);
            if (output.pbData != IntPtr.Zero) LocalFree(output.pbData);
        }
    }

    private sealed record Payload(string UserName, string Password);

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob
    {
        public int cbData;
        public IntPtr pbData;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(
        ref DataBlob pDataIn, string? szDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved,
        IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(
        ref DataBlob pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy, IntPtr pvReserved,
        IntPtr pPromptStruct, int dwFlags, out DataBlob pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
