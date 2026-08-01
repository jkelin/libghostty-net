using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace LibGhostty;

/// <summary>
/// Managed ABI bindings for the Ghostty VT release library.
/// </summary>
public sealed unsafe class GhosttyNativeLibrary : IDisposable
{
    public const int Success = 0;
    public const int TerminalOptionUserdata = 0;
    public const int TerminalOptionWritePty = 1;
    public const int TerminalOptionTitleChanged = 5;
    public const int TerminalOptionSize = 6;
    public const int TerminalOptionDeviceAttributes = 8;
    public const int TerminalDataWidthPixels = 16;
    public const int TerminalDataHeightPixels = 17;
    public const int TerminalOptionColorForeground = 11;
    public const int TerminalOptionColorBackground = 12;
    public const int KeyActionPress = 1;
    public const int KeyEncoderOptionAltEscPrefix = 3;
    public const int KeyEncoderOptionMacosOptionAsAlt = 6;
    public const int OptionAsAltTrue = 1;
    public const int PasteSuccess = 0;
    public const int RowDataWrapContinuation = 2;
    public const int RowCellsDataStyle = 2;
    public const int RenderStatePaletteLength = 256;
    public const int StyleColorTagPalette = 1;
    public const int StyleColorTagRgb = 2;
    public const int KeyBackquote = 1;
    public const int KeyBackslash = 2;
    public const int KeyBracketLeft = 3;
    public const int KeyBracketRight = 4;
    public const int KeyComma = 5;
    public const int KeyDigit0 = 6;
    public const int KeyDigit1 = 7;
    public const int KeyDigit9 = 15;
    public const int RowDataRaw = 2;
    public const int KeyEqual = 16;
    public const int KeyIntlBackslash = 17;
    public const int KeyIntlRo = 18;
    public const int KeyIntlYen = 19;
    public const int KeyA = 20;
    public const int KeyZ = 45;
    public const int KeyMinus = 46;
    public const int KeyPeriod = 47;
    public const int KeyQuote = 48;
    public const int KeySemicolon = 49;
    public const int KeySlash = 50;
    public const int KeyAltLeft = 51;
    public const int KeyAltRight = 52;
    public const int KeyBackspace = 53;
    public const int KeyCapsLock = 54;
    public const int KeyContextMenu = 55;
    public const int KeyControlLeft = 56;
    public const int KeyControlRight = 57;
    public const int KeyEnter = 58;
    public const int KeyMetaLeft = 59;
    public const int KeyMetaRight = 60;
    public const int KeyShiftLeft = 61;
    public const int KeyShiftRight = 62;
    public const int KeySpace = 63;
    public const int KeyTab = 64;
    public const int KeyDelete = 68;
    public const int KeyEnd = 69;
    public const int KeyHelp = 70;
    public const int KeyHome = 71;
    public const int KeyInsert = 72;
    public const int KeyPageDown = 73;
    public const int KeyPageUp = 74;
    public const int KeyArrowDown = 75;
    public const int KeyArrowLeft = 76;
    public const int KeyArrowRight = 77;
    public const int KeyArrowUp = 78;
    public const int KeyNumLock = 79;
    public const int KeyNumpad0 = 80;
    public const int KeyNumpadAdd = 90;
    public const int KeyNumpadDivide = 96;
    public const int KeyNumpadEnter = 97;
    public const int KeyNumpadMultiply = 104;
    public const int KeyNumpadSubtract = 107;
    public const int KeyNumpadDecimal = 95;
    public const int KeyEscape = 120;
    public const int KeyF1 = 121;
    public const int KeyF24 = 144;
    public const int TerminalDataTitle = 12;
    public const int TerminalDataActiveScreen = 6;
    public const int TerminalScreenAlternate = 1;
    public const int TerminalDataScrollbar = 9;
    public const int TerminalDataMouseTracking = 11;
    public const int MouseEventActionPress = 0;
    public const int MouseEventActionRelease = 1;
    public const int MouseEventActionMotion = 2;
    public const int MouseButtonNone = 0;
    public const int MouseButtonLeft = 1;
    public const int MouseButtonRight = 2;
    public const int MouseButtonMiddle = 3;
    public const int MouseButtonFour = 4;
    public const int MouseButtonFive = 5;
    public const int MouseEncoderOptionSize = 2;
    public const int MouseEncoderOptionAnyButtonPressed = 3;
    public const int OutOfSpace = -3;
    public const int ScrollViewportTop = 0;
    public const int ScrollViewportBottom = 1;
    public const int ScrollViewportDelta = 2;

    public const int RenderStateDataCols = 1;
    public const int RenderStateDataRows = 2;
    public const int RenderStateDataRowIterator = 4;
    public const int RenderStateDataColorBackground = 5;
    public const int RenderStateDataColorForeground = 6;
    public const int RenderStateDataCursorVisualStyle = 10;
    public const int RenderStateDataCursorVisible = 11;
    public const int RenderStateDataCursorViewportHasValue = 14;
    public const int RenderStateDataCursorViewportX = 15;
    public const int RenderStateDataCursorViewportY = 16;
    public const int RenderStateOptionDirty = 0;
    public const int RenderStateRowDataCells = 3;
    public const int RenderStateRowCellsDataRaw = 1;
    public const int RenderStateRowCellsDataGraphemesLength = 3;
    public const int RenderStateRowCellsDataGraphemesBuffer = 4;
    public const int RenderStateRowCellsDataBackgroundColor = 5;
    public const int RenderStateRowCellsDataForegroundColor = 6;
    public const int CellDataWide = 3;

    private readonly IntPtr _library;
    private readonly TerminalNewDelegate _terminalNew;
    private readonly TerminalFreeDelegate _terminalFree;
    private readonly TerminalResizeDelegate _terminalResize;
    private readonly TerminalSetDelegate _terminalSet;
    private readonly TerminalWriteDelegate _terminalWrite;
    private readonly TerminalGetTitleDelegate _terminalGetTitle;
    private readonly TerminalGetScrollbarDelegate _terminalGetScrollbar;
    private readonly TerminalGetBoolDelegate _terminalGetMouseTracking;
    private readonly TerminalGetIntDelegate _terminalGetActiveScreen;
    private readonly KeyEncoderNewDelegate _keyEncoderNew;
    private readonly KeyEncoderFreeDelegate _keyEncoderFree;
    private readonly KeyEncoderSetFromTerminalDelegate _keyEncoderSetFromTerminal;
    private readonly KeyEncoderEncodeDelegate _keyEncoderEncode;
    private readonly KeyEventNewDelegate _keyEventNew;
    private readonly KeyEventFreeDelegate _keyEventFree;
    private readonly KeyEventSetActionDelegate _keyEventSetAction;
    private readonly KeyEventSetKeyDelegate _keyEventSetKey;
    private readonly KeyEventSetModsDelegate _keyEventSetMods;
    private readonly KeyEventSetConsumedModsDelegate _keyEventSetConsumedMods;
    private readonly KeyEventSetUtf8Delegate _keyEventSetUtf8;
    private readonly KeyEventSetUnshiftedCodepointDelegate _keyEventSetUnshiftedCodepoint;
    private readonly PasteEncodeDelegate _pasteEncode;
    private readonly TerminalScrollViewportDelegate _terminalScrollViewport;
    private readonly MouseEventNewDelegate _mouseEventNew;
    private readonly MouseEventFreeDelegate _mouseEventFree;
    private readonly MouseEventSetActionDelegate _mouseEventSetAction;
    private readonly MouseEventSetButtonDelegate _mouseEventSetButton;
    private readonly MouseEventClearButtonDelegate _mouseEventClearButton;
    private readonly MouseEventSetModsDelegate _mouseEventSetMods;
    private readonly MouseEventSetPositionDelegate _mouseEventSetPosition;
    private readonly MouseEncoderNewDelegate _mouseEncoderNew;
    private readonly MouseEncoderFreeDelegate _mouseEncoderFree;
    private readonly MouseEncoderSetOptionDelegate _mouseEncoderSetOption;
    private readonly MouseEncoderSetFromTerminalDelegate _mouseEncoderSetFromTerminal;
    private readonly MouseEncoderEncodeDelegate _mouseEncoderEncode;

    private readonly RenderStateFreeDelegate _renderStateFree;
    private readonly RenderStateNewDelegate _renderStateNew;
    private readonly RenderStateUpdateDelegate _renderStateUpdate;
    private readonly RenderStateGetHandleDelegate _renderStateGetHandle;
    private readonly RenderStateGetColorDelegate _renderStateGetColor;
    private readonly RenderStateColorsGetDelegate _renderStateColorsGet;
    private readonly RenderStateGetU16Delegate _renderStateGetU16;
    private readonly RenderStateGetByteDelegate _renderStateGetByte;
    private readonly RenderStateGetIntDelegate _renderStateGetInt;
    private readonly RenderStateSetDirtyDelegate _renderStateSetDirty;
    private readonly RowIteratorNewDelegate _rowIteratorNew;
    private readonly RowIteratorFreeDelegate _rowIteratorFree;
    private readonly RowIteratorNextDelegate _rowIteratorNext;
    private readonly RowGetCellsDelegate _rowGetCells;
    private readonly RowGetRawDelegate _rowGetRaw;
    private readonly RowGetBoolDelegate _rowGetWrapContinuation;
    private readonly RowCellsNewDelegate _rowCellsNew;
    private readonly RowCellsFreeDelegate _rowCellsFree;
    private readonly RowCellsSelectDelegate _rowCellsSelect;
    private readonly RowCellsGetRawDelegate _rowCellsGetRaw;
    private readonly RowCellsGetU32Delegate _rowCellsGetU32;
    private readonly RowCellsGetStyleDelegate _rowCellsGetStyle;
    private readonly RowCellsGetColorDelegate _rowCellsGetColor;
    private readonly RowCellsGetBufferDelegate _rowCellsGetBuffer;
    private readonly CellGetWideDelegate _cellGetWide;
    private int _disposeState;

    public static GhosttyNativeLibrary LoadFromPackage() =>
        new(GhosttyRuntimeAssets.ResolveGhosttyLibrary());

    public GhosttyNativeLibrary(string libraryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(libraryPath);
        LibraryPath = Path.GetFullPath(libraryPath);
        _library = NativeLibrary.Load(LibraryPath);

        try
        {
            _terminalNew = Load<TerminalNewDelegate>("ghostty_terminal_new");
            _terminalFree = Load<TerminalFreeDelegate>("ghostty_terminal_free");
            _terminalResize = Load<TerminalResizeDelegate>("ghostty_terminal_resize");
            _terminalSet = Load<TerminalSetDelegate>("ghostty_terminal_set");
            _terminalWrite = Load<TerminalWriteDelegate>("ghostty_terminal_vt_write");
            _terminalGetTitle = Load<TerminalGetTitleDelegate>("ghostty_terminal_get");
            _terminalGetScrollbar = Load<TerminalGetScrollbarDelegate>("ghostty_terminal_get");
            _terminalGetMouseTracking = Load<TerminalGetBoolDelegate>("ghostty_terminal_get");
            _terminalGetActiveScreen = Load<TerminalGetIntDelegate>("ghostty_terminal_get");
            _keyEncoderNew = Load<KeyEncoderNewDelegate>("ghostty_key_encoder_new");
            _keyEncoderFree = Load<KeyEncoderFreeDelegate>("ghostty_key_encoder_free");
            _keyEncoderSetFromTerminal = Load<KeyEncoderSetFromTerminalDelegate>(
                "ghostty_key_encoder_setopt_from_terminal"
            );
            _keyEncoderEncode = Load<KeyEncoderEncodeDelegate>("ghostty_key_encoder_encode");
            _keyEventNew = Load<KeyEventNewDelegate>("ghostty_key_event_new");
            _keyEventFree = Load<KeyEventFreeDelegate>("ghostty_key_event_free");
            _keyEventSetAction = Load<KeyEventSetActionDelegate>("ghostty_key_event_set_action");
            _keyEventSetKey = Load<KeyEventSetKeyDelegate>("ghostty_key_event_set_key");
            _keyEventSetMods = Load<KeyEventSetModsDelegate>("ghostty_key_event_set_mods");
            _keyEventSetConsumedMods = Load<KeyEventSetConsumedModsDelegate>(
                "ghostty_key_event_set_consumed_mods"
            );
            _keyEventSetUtf8 = Load<KeyEventSetUtf8Delegate>("ghostty_key_event_set_utf8");
            _keyEventSetUnshiftedCodepoint = Load<KeyEventSetUnshiftedCodepointDelegate>(
                "ghostty_key_event_set_unshifted_codepoint"
            );
            _pasteEncode = Load<PasteEncodeDelegate>("ghostty_paste_encode");
            _mouseEventNew = Load<MouseEventNewDelegate>("ghostty_mouse_event_new");
            _mouseEventFree = Load<MouseEventFreeDelegate>("ghostty_mouse_event_free");
            _mouseEventSetAction = Load<MouseEventSetActionDelegate>(
                "ghostty_mouse_event_set_action"
            );
            _mouseEventSetButton = Load<MouseEventSetButtonDelegate>(
                "ghostty_mouse_event_set_button"
            );
            _mouseEventClearButton = Load<MouseEventClearButtonDelegate>(
                "ghostty_mouse_event_clear_button"
            );
            _mouseEventSetMods = Load<MouseEventSetModsDelegate>("ghostty_mouse_event_set_mods");
            _mouseEventSetPosition = Load<MouseEventSetPositionDelegate>(
                "ghostty_mouse_event_set_position"
            );
            _mouseEncoderNew = Load<MouseEncoderNewDelegate>("ghostty_mouse_encoder_new");
            _mouseEncoderFree = Load<MouseEncoderFreeDelegate>("ghostty_mouse_encoder_free");
            _mouseEncoderSetOption = Load<MouseEncoderSetOptionDelegate>(
                "ghostty_mouse_encoder_setopt"
            );
            _mouseEncoderSetFromTerminal = Load<MouseEncoderSetFromTerminalDelegate>(
                "ghostty_mouse_encoder_setopt_from_terminal"
            );
            _mouseEncoderEncode = Load<MouseEncoderEncodeDelegate>("ghostty_mouse_encoder_encode");
            _terminalScrollViewport = Load<TerminalScrollViewportDelegate>(
                "ghostty_terminal_scroll_viewport"
            );

            _renderStateFree = Load<RenderStateFreeDelegate>("ghostty_render_state_free");
            _renderStateNew = Load<RenderStateNewDelegate>("ghostty_render_state_new");
            _renderStateUpdate = Load<RenderStateUpdateDelegate>("ghostty_render_state_update");
            _renderStateGetHandle = Load<RenderStateGetHandleDelegate>("ghostty_render_state_get");
            _renderStateGetColor = Load<RenderStateGetColorDelegate>("ghostty_render_state_get");
            _renderStateColorsGet = Load<RenderStateColorsGetDelegate>(
                "ghostty_render_state_colors_get"
            );
            _renderStateGetU16 = Load<RenderStateGetU16Delegate>("ghostty_render_state_get");
            _renderStateGetByte = Load<RenderStateGetByteDelegate>("ghostty_render_state_get");
            _renderStateGetInt = Load<RenderStateGetIntDelegate>("ghostty_render_state_get");
            _renderStateSetDirty = Load<RenderStateSetDirtyDelegate>("ghostty_render_state_set");
            _rowIteratorNew = Load<RowIteratorNewDelegate>("ghostty_render_state_row_iterator_new");
            _rowIteratorFree = Load<RowIteratorFreeDelegate>(
                "ghostty_render_state_row_iterator_free"
            );
            _rowIteratorNext = Load<RowIteratorNextDelegate>(
                "ghostty_render_state_row_iterator_next"
            );
            _rowGetCells = Load<RowGetCellsDelegate>("ghostty_render_state_row_get");
            _rowGetRaw = Load<RowGetRawDelegate>("ghostty_render_state_row_get");
            _rowGetWrapContinuation = Load<RowGetBoolDelegate>("ghostty_row_get");
            _rowCellsNew = Load<RowCellsNewDelegate>("ghostty_render_state_row_cells_new");
            _rowCellsFree = Load<RowCellsFreeDelegate>("ghostty_render_state_row_cells_free");
            _rowCellsSelect = Load<RowCellsSelectDelegate>("ghostty_render_state_row_cells_select");
            _rowCellsGetRaw = Load<RowCellsGetRawDelegate>("ghostty_render_state_row_cells_get");
            _rowCellsGetStyle = Load<RowCellsGetStyleDelegate>(
                "ghostty_render_state_row_cells_get"
            );
            _rowCellsGetU32 = Load<RowCellsGetU32Delegate>("ghostty_render_state_row_cells_get");
            _rowCellsGetColor = Load<RowCellsGetColorDelegate>(
                "ghostty_render_state_row_cells_get"
            );
            _rowCellsGetBuffer = Load<RowCellsGetBufferDelegate>(
                "ghostty_render_state_row_cells_get"
            );
            _cellGetWide = Load<CellGetWideDelegate>("ghostty_cell_get");
        }
        catch
        {
            NativeLibrary.Free(_library);
            throw;
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void WritePtyCallback(
        IntPtr terminal,
        IntPtr userdata,
        IntPtr data,
        nuint length
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void TitleChangedCallback(IntPtr terminal, IntPtr userdata);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte SizeCallback(IntPtr terminal, IntPtr userdata, IntPtr output);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate byte DeviceAttributesCallback(
        IntPtr terminal,
        IntPtr userdata,
        IntPtr output
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TerminalFreeDelegate(IntPtr terminal);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RenderStateFreeDelegate(IntPtr state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RowIteratorFreeDelegate(IntPtr iterator);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void RowCellsFreeDelegate(IntPtr cells);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalNewDelegate(
        IntPtr allocator,
        out IntPtr terminal,
        TerminalOptions options
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TerminalWriteDelegate(IntPtr terminal, IntPtr data, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalResizeDelegate(
        IntPtr terminal,
        ushort columns,
        ushort rows,
        uint cellWidth,
        uint cellHeight
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalSetDelegate(IntPtr terminal, int option, IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalGetTitleDelegate(
        IntPtr terminal,
        int data,
        out NativeString title
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalGetScrollbarDelegate(
        IntPtr terminal,
        int data,
        out NativeScrollbar scrollbar
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalGetBoolDelegate(IntPtr terminal, int data, out byte value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int TerminalGetIntDelegate(IntPtr terminal, int data, out int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int KeyEncoderNewDelegate(IntPtr allocator, out IntPtr encoder);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEncoderFreeDelegate(IntPtr encoder);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEncoderSetFromTerminalDelegate(IntPtr encoder, IntPtr terminal);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int KeyEncoderEncodeDelegate(
        IntPtr encoder,
        IntPtr eventHandle,
        IntPtr buffer,
        nuint bufferSize,
        out nuint written
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int KeyEventNewDelegate(IntPtr allocator, out IntPtr eventHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventFreeDelegate(IntPtr eventHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventSetActionDelegate(IntPtr eventHandle, int action);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventSetKeyDelegate(IntPtr eventHandle, int key);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventSetModsDelegate(IntPtr eventHandle, ushort modifiers);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventSetConsumedModsDelegate(IntPtr eventHandle, ushort modifiers);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventSetUtf8Delegate(IntPtr eventHandle, IntPtr utf8, nuint length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void KeyEventSetUnshiftedCodepointDelegate(IntPtr eventHandle, uint codepoint);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int PasteEncodeDelegate(
        IntPtr data,
        nuint dataLength,
        byte bracketed,
        IntPtr buffer,
        nuint bufferLength,
        out nuint written
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MouseEventNewDelegate(IntPtr allocator, out IntPtr eventHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEventFreeDelegate(IntPtr eventHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEventSetActionDelegate(IntPtr eventHandle, int action);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEventClearButtonDelegate(IntPtr eventHandle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEventSetButtonDelegate(IntPtr eventHandle, int button);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEventSetModsDelegate(IntPtr eventHandle, ushort modifiers);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEventSetPositionDelegate(
        IntPtr eventHandle,
        NativeMousePosition position
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MouseEncoderNewDelegate(IntPtr allocator, out IntPtr encoder);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEncoderFreeDelegate(IntPtr encoder);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEncoderSetOptionDelegate(IntPtr encoder, int option, IntPtr value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void MouseEncoderSetFromTerminalDelegate(IntPtr encoder, IntPtr terminal);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int MouseEncoderEncodeDelegate(
        IntPtr encoder,
        IntPtr eventHandle,
        IntPtr buffer,
        nuint bufferSize,
        out nuint written
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void TerminalScrollViewportDelegate(
        IntPtr terminal,
        TerminalScrollViewport behavior
    );

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateNewDelegate(IntPtr allocator, out IntPtr state);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateUpdateDelegate(IntPtr state, IntPtr terminal);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateGetHandleDelegate(IntPtr state, int data, ref IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateGetColorDelegate(IntPtr state, int data, out NativeColor color);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateColorsGetDelegate(IntPtr state, IntPtr colors);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateGetU16Delegate(IntPtr state, int data, out ushort value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateGetByteDelegate(IntPtr state, int data, out byte value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateGetIntDelegate(IntPtr state, int data, out int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RenderStateSetDirtyDelegate(IntPtr state, int option, ref int value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowIteratorNewDelegate(IntPtr allocator, out IntPtr iterator);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate byte RowIteratorNextDelegate(IntPtr iterator);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowGetCellsDelegate(IntPtr iterator, int data, ref IntPtr cells);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowGetRawDelegate(IntPtr iterator, int data, out ulong rawRow);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowGetBoolDelegate(ulong rawRow, int data, out byte value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsNewDelegate(IntPtr allocator, out IntPtr cells);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsSelectDelegate(IntPtr cells, ushort column);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsGetRawDelegate(IntPtr cells, int data, out ulong rawCell);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsGetU32Delegate(IntPtr cells, int data, out uint value);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsGetColorDelegate(IntPtr cells, int data, out NativeColor color);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsGetStyleDelegate(IntPtr cells, int data, ref NativeStyle style);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RowCellsGetBufferDelegate(IntPtr cells, int data, IntPtr buffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CellGetWideDelegate(ulong cell, int data, out int wide);

    public string LibraryPath { get; }

    public static IntPtr GetCallbackPointer<T>(T callback)
        where T : Delegate => Marshal.GetFunctionPointerForDelegate(callback);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) == 0)
        {
            NativeLibrary.Free(_library);
        }

        GC.SuppressFinalize(this);
    }

    public int TerminalNew(TerminalOptions options, out IntPtr terminal) =>
        _terminalNew(IntPtr.Zero, out terminal, options);

    public void TerminalFree(IntPtr terminal) => _terminalFree(terminal);

    public int TerminalResize(
        IntPtr terminal,
        ushort columns,
        ushort rows,
        uint cellWidth,
        uint cellHeight
    ) => _terminalResize(terminal, columns, rows, cellWidth, cellHeight);

    public int TerminalSet(IntPtr terminal, int option, IntPtr value) =>
        _terminalSet(terminal, option, value);

    public int KeyEncoderNew(out IntPtr encoder) => _keyEncoderNew(IntPtr.Zero, out encoder);

    public void KeyEncoderFree(IntPtr encoder) => _keyEncoderFree(encoder);

    public void KeyEncoderSetFromTerminal(IntPtr encoder, IntPtr terminal) =>
        _keyEncoderSetFromTerminal(encoder, terminal);

    public int KeyEncoderEncode(
        IntPtr encoder,
        IntPtr eventHandle,
        IntPtr buffer,
        nuint bufferSize,
        out nuint written
    ) => _keyEncoderEncode(encoder, eventHandle, buffer, bufferSize, out written);

    public int KeyEventNew(out IntPtr eventHandle) => _keyEventNew(IntPtr.Zero, out eventHandle);

    public void KeyEventFree(IntPtr eventHandle) => _keyEventFree(eventHandle);

    public void KeyEventSetAction(IntPtr eventHandle, int action) =>
        _keyEventSetAction(eventHandle, action);

    public void KeyEventSetKey(IntPtr eventHandle, int key) => _keyEventSetKey(eventHandle, key);

    public void KeyEventSetMods(IntPtr eventHandle, ushort modifiers) =>
        _keyEventSetMods(eventHandle, modifiers);

    public void KeyEventSetConsumedMods(IntPtr eventHandle, ushort modifiers) =>
        _keyEventSetConsumedMods(eventHandle, modifiers);

    public void KeyEventSetUtf8(IntPtr eventHandle, IntPtr utf8, nuint length) =>
        _keyEventSetUtf8(eventHandle, utf8, length);

    public void KeyEventSetUnshiftedCodepoint(IntPtr eventHandle, uint codepoint) =>
        _keyEventSetUnshiftedCodepoint(eventHandle, codepoint);

    public int PasteEncode(
        IntPtr data,
        nuint dataLength,
        bool bracketed,
        IntPtr buffer,
        nuint bufferLength,
        out nuint written
    ) =>
        _pasteEncode(
            data,
            dataLength,
            bracketed ? (byte)1 : (byte)0,
            buffer,
            bufferLength,
            out written
        );

    public void TerminalWrite(IntPtr terminal, IntPtr data, nuint length) =>
        _terminalWrite(terminal, data, length);

    public int TerminalGetTitle(IntPtr terminal, out NativeString title) =>
        _terminalGetTitle(terminal, TerminalDataTitle, out title);

    public int TerminalGetScrollbar(IntPtr terminal, out NativeScrollbar scrollbar) =>
        _terminalGetScrollbar(terminal, TerminalDataScrollbar, out scrollbar);

    public int TerminalGetMouseTracking(IntPtr terminal, out byte enabled) =>
        _terminalGetMouseTracking(terminal, TerminalDataMouseTracking, out enabled);

    public int TerminalGetActiveScreen(IntPtr terminal, out int screen) =>
        _terminalGetActiveScreen(terminal, TerminalDataActiveScreen, out screen);

    public int MouseEventNew(out IntPtr eventHandle) =>
        _mouseEventNew(IntPtr.Zero, out eventHandle);

    public void MouseEventFree(IntPtr eventHandle) => _mouseEventFree(eventHandle);

    public void MouseEventSetAction(IntPtr eventHandle, int action) =>
        _mouseEventSetAction(eventHandle, action);

    public void MouseEventSetButton(IntPtr eventHandle, int button) =>
        _mouseEventSetButton(eventHandle, button);

    public void MouseEventClearButton(IntPtr eventHandle) => _mouseEventClearButton(eventHandle);

    public void MouseEventSetMods(IntPtr eventHandle, ushort modifiers) =>
        _mouseEventSetMods(eventHandle, modifiers);

    public void MouseEventSetPosition(IntPtr eventHandle, NativeMousePosition position) =>
        _mouseEventSetPosition(eventHandle, position);

    public int MouseEncoderNew(out IntPtr encoder) => _mouseEncoderNew(IntPtr.Zero, out encoder);

    public void MouseEncoderFree(IntPtr encoder) => _mouseEncoderFree(encoder);

    public void MouseEncoderSetOption(IntPtr encoder, int option, ref NativeMouseEncoderSize size)
    {
        fixed (NativeMouseEncoderSize* pointer = &size)
        {
            _mouseEncoderSetOption(encoder, option, (IntPtr)pointer);
        }
    }

    public void MouseEncoderSetByteOption(IntPtr encoder, int option, byte value)
    {
        _mouseEncoderSetOption(encoder, option, (IntPtr)(&value));
    }

    public void MouseEncoderSetFromTerminal(IntPtr encoder, IntPtr terminal) =>
        _mouseEncoderSetFromTerminal(encoder, terminal);

    public int MouseEncoderEncode(
        IntPtr encoder,
        IntPtr eventHandle,
        IntPtr buffer,
        nuint bufferSize,
        out nuint written
    ) => _mouseEncoderEncode(encoder, eventHandle, buffer, bufferSize, out written);

    public void ScrollViewport(IntPtr terminal, TerminalScrollViewport behavior) =>
        _terminalScrollViewport(terminal, behavior);

    public int RenderStateNew(out IntPtr state) => _renderStateNew(IntPtr.Zero, out state);

    public void RenderStateFree(IntPtr state) => _renderStateFree(state);

    public int RenderStateUpdate(IntPtr state, IntPtr terminal) =>
        _renderStateUpdate(state, terminal);

    public int RowCellsGetStyle(IntPtr cells, out NativeStyle style)
    {
        style = new NativeStyle { Size = (nuint)sizeof(NativeStyle) };
        return _rowCellsGetStyle(cells, RowCellsDataStyle, ref style);
    }

    public int RenderStateGetHandle(IntPtr state, int data, ref IntPtr handle) =>
        _renderStateGetHandle(state, data, ref handle);

    public int RenderStateGetColor(IntPtr state, int data, out NativeColor color) =>
        _renderStateGetColor(state, data, out color);

    public int RenderStateColorsGet(IntPtr state, NativeColor[] palette)
    {
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Length != RenderStatePaletteLength)
        {
            throw new ArgumentException(
                "The Ghostty palette must contain 256 colors.",
                nameof(palette)
            );
        }

        NativeRenderStateColors* pointer = stackalloc NativeRenderStateColors[1];
        *pointer = new NativeRenderStateColors { Size = (nuint)sizeof(NativeRenderStateColors) };
        var result = _renderStateColorsGet(state, (IntPtr)pointer);
        if (result == Success)
        {
            for (var index = 0; index < RenderStatePaletteLength; index++)
            {
                var offset = index * 3;
                palette[index] = new NativeColor
                {
                    Red = pointer->Palette[offset],
                    Green = pointer->Palette[offset + 1],
                    Blue = pointer->Palette[offset + 2],
                };
            }
        }

        return result;
    }

    public int RenderStateGetU16(IntPtr state, int data, out ushort value) =>
        _renderStateGetU16(state, data, out value);

    public int RenderStateGetByte(IntPtr state, int data, out byte value) =>
        _renderStateGetByte(state, data, out value);

    public int RenderStateGetInt(IntPtr state, int data, out int value) =>
        _renderStateGetInt(state, data, out value);

    public int RenderStateSetDirty(IntPtr state, int option, ref int value) =>
        _renderStateSetDirty(state, option, ref value);

    public int RowIteratorNew(out IntPtr iterator) => _rowIteratorNew(IntPtr.Zero, out iterator);

    public void RowIteratorFree(IntPtr iterator) => _rowIteratorFree(iterator);

    public bool RowIteratorNext(IntPtr iterator) => _rowIteratorNext(iterator) != 0;

    public int RowGetRaw(IntPtr iterator, out ulong rawRow) =>
        _rowGetRaw(iterator, RowDataRaw, out rawRow);

    public int RowGetWrapContinuation(ulong rawRow, out byte value) =>
        _rowGetWrapContinuation(rawRow, RowDataWrapContinuation, out value);

    public int RowGetCells(IntPtr iterator, ref IntPtr cells) =>
        _rowGetCells(iterator, RenderStateRowDataCells, ref cells);

    public int RowCellsNew(out IntPtr cells) => _rowCellsNew(IntPtr.Zero, out cells);

    public void RowCellsFree(IntPtr cells) => _rowCellsFree(cells);

    public int RowCellsSelect(IntPtr cells, ushort column) => _rowCellsSelect(cells, column);

    public int RowCellsGetRaw(IntPtr cells, out ulong rawCell) =>
        _rowCellsGetRaw(cells, RenderStateRowCellsDataRaw, out rawCell);

    public int RowCellsGetU32(IntPtr cells, int data, out uint value) =>
        _rowCellsGetU32(cells, data, out value);

    public int RowCellsGetColor(IntPtr cells, int data, out NativeColor color) =>
        _rowCellsGetColor(cells, data, out color);

    public int RowCellsGetBuffer(IntPtr cells, int data, IntPtr buffer) =>
        _rowCellsGetBuffer(cells, data, buffer);

    public int CellGetWide(ulong rawCell, out int wide) =>
        _cellGetWide(rawCell, CellDataWide, out wide);

    private T Load<T>(string name)
        where T : Delegate
    {
        try
        {
            return Marshal.GetDelegateForFunctionPointer<T>(
                NativeLibrary.GetExport(_library, name)
            );
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or ArgumentException)
        {
            throw new EntryPointNotFoundException(
                $"Native Ghostty library at '{LibraryPath}' does not export '{name}'.",
                ex
            );
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerminalOptions
    {
        public ushort Columns;
        public ushort Rows;
        public nuint MaxScrollback;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeColor
    {
        public byte Red;
        public byte Green;
        public byte Blue;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeRenderStateColors
    {
        public nuint Size;
        public NativeColor Background;
        public NativeColor Foreground;
        public NativeColor Cursor;
        public byte CursorHasValue;
        public fixed byte Palette[768];
    }

    [StructLayout(LayoutKind.Explicit, Size = 72)]
    public struct NativeStyle
    {
        [FieldOffset(0)]
        public nuint Size;

        [FieldOffset(8)]
        public NativeStyleColor Foreground;

        [FieldOffset(24)]
        public NativeStyleColor Background;

        [FieldOffset(40)]
        public NativeStyleColor UnderlineColor;

        [FieldOffset(56)]
        public byte Bold;

        [FieldOffset(57)]
        public byte Italic;

        [FieldOffset(58)]
        public byte Faint;

        [FieldOffset(59)]
        public byte Blink;

        [FieldOffset(60)]
        public byte Inverse;

        [FieldOffset(61)]
        public byte Invisible;

        [FieldOffset(62)]
        public byte Strikethrough;

        [FieldOffset(63)]
        public byte Overline;

        [FieldOffset(64)]
        public int Underline;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct NativeStyleColor
    {
        [FieldOffset(0)]
        public int Tag;

        [FieldOffset(8)]
        public NativeStyleColorValue Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct NativeStyleColorValue
    {
        [FieldOffset(0)]
        public byte Palette;

        [FieldOffset(0)]
        public NativeColor Rgb;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeSizeReport
    {
        public ushort Rows;
        public ushort Columns;
        public uint CellWidth;
        public uint CellHeight;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeDeviceAttributes
    {
        public NativeDeviceAttributesPrimary Primary;
        public NativeDeviceAttributesSecondary Secondary;
        public NativeDeviceAttributesTertiary Tertiary;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct NativeDeviceAttributesPrimary
    {
        public ushort ConformanceLevel;
        public fixed ushort Features[64];
        public nuint FeatureCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDeviceAttributesSecondary
    {
        public ushort DeviceType;
        public ushort FirmwareVersion;
        public ushort RomCartridge;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeDeviceAttributesTertiary
    {
        public uint UnitId;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeString
    {
        public IntPtr Pointer;
        public nuint Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeScrollbar
    {
        public ulong Total;
        public ulong Offset;
        public ulong Length;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMousePosition
    {
        public float X;
        public float Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NativeMouseEncoderSize
    {
        public nuint Size;
        public uint ScreenWidth;
        public uint ScreenHeight;
        public uint CellWidth;
        public uint CellHeight;
        public uint PaddingTop;
        public uint PaddingBottom;
        public uint PaddingRight;
        public uint PaddingLeft;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct TerminalScrollViewport
    {
        public int Tag;
        public TerminalScrollViewportValue Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct TerminalScrollViewportValue
    {
        [FieldOffset(0)]
        public IntPtr Delta;
    }
}
