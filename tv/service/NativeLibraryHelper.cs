using System;
using System.Runtime.InteropServices;

namespace Service
{
    static class NativeLibraryHelper
    {
        [DllImport("libdl.so.2")] private static extern IntPtr dlopen(string filename, int flags);
        [DllImport("libdl.so.2")] private static extern IntPtr dlerror();
        private const int RtldNow = 2;

        public static IntPtr Load(string name, out string error)
        {
            error = null;
            try
            {
                dlerror();
                IntPtr library = dlopen(name, RtldNow);
                if (library != IntPtr.Zero) return library;
                string nativeError = Marshal.PtrToStringAnsi(dlerror());
                if (string.IsNullOrWhiteSpace(nativeError)) nativeError = null;
                try
                {
                    NativeLibrary.Free(NativeLibrary.Load(name));
                    error = $"dlopen(RTLD_NOW) failed: {nativeError ?? "no error text"} " +
                        "(a lazy load succeeded; an unresolved symbol or a different search path may be responsible)";
                }
                catch (Exception ex) { error = nativeError ?? ex.Message; }
            }
            catch (Exception ex) { error = ex.Message; }
            return IntPtr.Zero;
        }
    }
}
