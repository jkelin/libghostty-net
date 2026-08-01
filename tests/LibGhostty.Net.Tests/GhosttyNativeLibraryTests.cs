using System.Runtime.InteropServices;
using System.Text;
using LibGhostty;

namespace LibGhostty.Net.Tests;

public sealed class GhosttyNativeLibraryTests
{
    [Fact]
    public void ConstructorRejectsWhitespacePath()
    {
        Assert.Throws<ArgumentException>(() => new GhosttyNativeLibrary("  "));
    }

    [Fact]
    public void TerminalAndRenderStateLifecycleWorks()
    {
        using var library = new GhosttyNativeLibrary(TestAssetLocator.RequireGhosttyLibrary());
        var result = library.TerminalNew(
            new GhosttyNativeLibrary.TerminalOptions
            {
                Columns = 80,
                Rows = 24,
                MaxScrollback = 256,
            },
            out var terminal
        );
        Assert.Equal(GhosttyNativeLibrary.Success, result);
        Assert.NotEqual(IntPtr.Zero, terminal);

        try
        {
            Assert.Equal(GhosttyNativeLibrary.Success, library.TerminalResize(terminal, 100, 30, 8, 16));

            var text = Encoding.UTF8.GetBytes("native-test\r\n");
            var textPointer = Marshal.AllocHGlobal(text.Length);
            try
            {
                Marshal.Copy(text, 0, textPointer, text.Length);
                library.TerminalWrite(terminal, textPointer, (nuint)text.Length);
            }
            finally
            {
                Marshal.FreeHGlobal(textPointer);
            }

            result = library.RenderStateNew(out var renderState);
            Assert.Equal(GhosttyNativeLibrary.Success, result);
            Assert.NotEqual(IntPtr.Zero, renderState);
            try
            {
                Assert.Equal(
                    GhosttyNativeLibrary.Success,
                    library.RenderStateUpdate(renderState, terminal)
                );

                var palette = new GhosttyNativeLibrary.NativeColor[
                    GhosttyNativeLibrary.RenderStatePaletteLength
                ];
                Assert.Equal(
                    GhosttyNativeLibrary.Success,
                    library.RenderStateColorsGet(renderState, palette)
                );
            }
            finally
            {
                library.RenderStateFree(renderState);
            }
        }
        finally
        {
            library.TerminalFree(terminal);
        }
    }

    [Fact]
    public void KeyEncoderAndPasteEncoderProduceOutput()
    {
        using var library = new GhosttyNativeLibrary(TestAssetLocator.RequireGhosttyLibrary());
        Assert.Equal(GhosttyNativeLibrary.Success, library.TerminalNew(
            new GhosttyNativeLibrary.TerminalOptions { Columns = 80, Rows = 24 },
            out var terminal
        ));
        try
        {
            Assert.Equal(GhosttyNativeLibrary.Success, library.KeyEncoderNew(out var encoder));
            try
            {
                library.KeyEncoderSetFromTerminal(encoder, terminal);
                Assert.Equal(GhosttyNativeLibrary.Success, library.KeyEventNew(out var keyEvent));
                try
                {
                    library.KeyEventSetAction(keyEvent, GhosttyNativeLibrary.KeyActionPress);
                    library.KeyEventSetKey(keyEvent, GhosttyNativeLibrary.KeyA);
                    library.KeyEventSetMods(keyEvent, 0);
                    library.KeyEventSetConsumedMods(keyEvent, 0);
                    library.KeyEventSetUnshiftedCodepoint(keyEvent, (uint)'a');

                    var utf8 = Marshal.AllocHGlobal(1);
                    try
                    {
                        Marshal.WriteByte(utf8, (byte)'a');
                        library.KeyEventSetUtf8(keyEvent, utf8, 1);

                        var buffer = Marshal.AllocHGlobal(64);
                        try
                        {
                            var result = library.KeyEncoderEncode(
                                encoder,
                                keyEvent,
                                buffer,
                                64,
                                out var written
                            );
                            Assert.Equal(GhosttyNativeLibrary.Success, result);
                            Assert.InRange(written, (nuint)1, (nuint)64);
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(buffer);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(utf8);
                    }
                }
                finally
                {
                    library.KeyEventFree(keyEvent);
                }
            }
            finally
            {
                library.KeyEncoderFree(encoder);
            }

            var paste = Encoding.UTF8.GetBytes("paste-test");
            var pastePointer = Marshal.AllocHGlobal(paste.Length);
            var pasteBuffer = Marshal.AllocHGlobal(128);
            try
            {
                Marshal.Copy(paste, 0, pastePointer, paste.Length);
                var result = library.PasteEncode(
                    pastePointer,
                    (nuint)paste.Length,
                    bracketed: false,
                    pasteBuffer,
                    128,
                    out var written
                );
                Assert.Equal(GhosttyNativeLibrary.PasteSuccess, result);
                Assert.Equal((nuint)paste.Length, written);
                var encoded = new byte[(int)written];
                Marshal.Copy(pasteBuffer, encoded, 0, encoded.Length);
                Assert.Equal(paste, encoded);
            }
            finally
            {
                Marshal.FreeHGlobal(pasteBuffer);
                Marshal.FreeHGlobal(pastePointer);
            }
        }
        finally
        {
            library.TerminalFree(terminal);
        }
    }

    [Fact]
    public void RenderStateColorsValidatesPaletteLengthBeforeNativeCall()
    {
        using var library = new GhosttyNativeLibrary(TestAssetLocator.RequireGhosttyLibrary());

        Assert.Throws<ArgumentException>(
            () => library.RenderStateColorsGet(IntPtr.Zero, new GhosttyNativeLibrary.NativeColor[1])
        );
    }

    [Fact]
    public void DisposeIsIdempotent()
    {
        var library = new GhosttyNativeLibrary(TestAssetLocator.RequireGhosttyLibrary());

        library.Dispose();
        library.Dispose();
    }
}
