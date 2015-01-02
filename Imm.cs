using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace Arith.Input.Text
{

    /// <summary>
    /// IME の変換モード。
    /// </summary>
    [Flags]
    public enum ImeConversionMode : uint
    {
        /// <summary>
        /// 英数モード
        /// </summary>
        AlphaNumeric = 0x0000,
        /// <summary>
        /// 対応言語入力モード
        /// </summary>
        Native = 0x0001,
        /// <summary>
        /// カタカナモード
        /// </summary>
        Katakana = 0x0002,
        /// <summary>
        /// 全角モード
        /// </summary>
        FullShape = 0x0008,
        /// <summary>
        /// ローマ字入力モード
        /// </summary>
        Roman = 0x0010,
        /// <summary>
        /// キャラクタ入力モード
        /// </summary>
        CharCode = 0x0020,
        /// <summary>
        /// ハングル文字変換モード
        /// </summary>
        HanjaConvert = 0x0040,
        /// <summary>
        /// ソフトウェアキーボードモード
        /// </summary>
        SoftKeyboard = 0x0080,
        /// <summary>
        /// 無変換モード
        /// </summary>
        NoConversion = 0x0100,
        /// <summary>
        /// EUD 変換モード?
        /// </summary>
        EUDC = 0x0200,
        /// <summary>
        /// シンボルモード
        /// </summary>
        Symbol = 0x0400,
    }

    /// <summary>
    /// IMM による IME アクセスを提供します。
    /// </summary>
    public class Imm
    {
        #region enum

        [Flags]
        enum IACE : uint
        {
            CHILDREN = 0x0001,
            DEFAULT = 0x0010,
            IGNORENOCONTEXT = 0x0020
        }

        enum GCS : uint
        {
            COMPATTR = 0x10,
            COMPCLAUSE = 0x20,
            COMPREADATTR = 0x2,
            COMPREADCLAUSE = 0x4,
            COMPREADSTR = 0x1,
            COMPSTR = 0x8,
            CURSORPOS = 0x80,
            DELTASTART = 0x100,
            RESULTCLAUSE = 0x1000,
            RESULTREADCLAUSE = 0x400,
            RESULTREADSTR = 0x200,
            RESULTSTR = 0x800,
        }

        enum IMN : int
        {
            CLOSESTATUSWINDOW = 0x0001,
            OPENSTATUSWINDOW = 0x0002,
            CHANGECANDIDATE = 0x0003,
            CLOSECANDIDATE = 0x0004,
            SETCONVERSIONMODE = 0x0005,
            SETSENTENCEMODE = 0x0006,
            SETOPENSTATUS = 0x0008,
            SETCANDIDATEPOS = 0x0009,
            SETCOMPOSITIONFONT = 0x000a,
            SETCOMPOSITIONWINDOW = 0x000b,
            SETSTATUSWINDOWPOS = 0x000c,
            GUIDELINE = 0x000d,
            PRIVATE = 0x000e,
        }
        #endregion

        #region Window Messages
        const int WM_CHAR = 0x0102;
        const int WM_IME_SETCONTEXT = 0x0281;
        const int WM_IME_NOTIFY = 0x0282;
        const int WM_IME_STARTCOMPOSITION = 0x010d;
        const int WM_IME_ENDCOMPOSITION = 0x010e;
        const int WM_IME_COMPOSITION = 0x010f;
        #endregion

        #region WINAPI

        [DllImport("user32")]
        static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("imm32")]
        static extern bool ImmAssociateContextEx(IntPtr hWnd, IntPtr hIMC, IACE dwFlags);

        [DllImport("imm32")]
        static extern IntPtr ImmGetContext(IntPtr hWnd);

        [DllImport("imm32")]
        static extern IntPtr ImmReleaseContext(IntPtr hWnd, IntPtr hIMC);

        [DllImport("imm32")]
        static extern int ImmGetCompositionString(IntPtr hIMC, GCS dwIndex, byte[] buf, uint dwBufLen);

        [DllImport("imm32")]
        static extern int ImmGetCandidateList(IntPtr hIMC, uint dwIndex, sbyte[] buf, uint dwBufLen);

        [DllImport("imm32")]
        static extern int ImmGetOpenStatus(IntPtr hIMC);

        [DllImport("imm32")]
        static extern uint ImmGetDescriptionW(IntPtr hKL, byte[] lpszDescription, uint uBufLen);

        [DllImport("imm32")]
        static extern int ImmGetConversionStatus(IntPtr hIMC, out uint lpfdwConversion, out uint lpfdwSentence);

        #endregion

        /// <summary>
        /// <see cref="Imm"/> を初期化します。
        /// </summary>
        /// <param name="control">IME を操作する対象のコントロール</param>
        public Imm(Control control)
        {
            Control = control;
        }

        /// <summary>
        /// 対象となっているコントロールを取得します。
        /// </summary>
        public Control Control { get; private set; }
        IntPtr hWnd { get { return Control.Handle; } }
        
        string GetCompositionString(GCS dwIndex)
        {
            return Lock(ctx =>
            {
                var sz = ImmGetCompositionString(ctx, dwIndex, null, 0);
                var buf = new byte[sz];
                ImmGetCompositionString(ctx, dwIndex, buf, (uint)sz);
                return Encoding.Default.GetString(buf);
            });
        }

        void Lock(Action<IntPtr> action)
        {
            var hContext = ImmGetContext(hWnd);
            action(hContext);
            ImmReleaseContext(hWnd, hContext);
        }

        T Lock<T>(Func<IntPtr, T> action)
        {
            var hContext = ImmGetContext(hWnd);
            try
            {
                return action(hContext);
            }
            finally
            {
                ImmReleaseContext(hWnd, hContext);
            }
        }

        /// <summary>
        /// 現在入力中の文字列を取得します。
        /// </summary>
        public string Composition { get { return GetCompositionString(GCS.COMPSTR); } }
        /// <summary>
        /// 直前に完了した入力文字列を取得します。
        /// </summary>
        public string Result { get { return GetCompositionString(GCS.RESULTSTR); } }
        /// <summary>
        /// IME が有効になっているか否かを取得します。
        /// </summary>
        public bool OpenStatus { get { return Lock(ctx => ImmGetOpenStatus(ctx))!= 0; } }
        /// <summary>
        /// IMEのモードを取得します。
        /// </summary>
        public ImeConversionMode ConversionMode
        {
            get
            {
                return Lock(ctx =>
                {
                    uint dmy, cmd;
                    ImmGetConversionStatus(ctx, out cmd, out dmy);
                    return (ImeConversionMode)cmd;
                });
            }
        }

        /// <summary>
        /// IME の説明を取得します。
        /// </summary>
        public string Description
        {
            get
            {
                var hKL = GetKeyboardLayout(0);
                var sz =  ImmGetDescriptionW(hKL, null, 0);
                var buf = new byte[sz];
                ImmGetDescriptionW(hKL, buf, sz);
                return Encoding.Unicode.GetString(buf);
            }
        }

        /// <summary>
        /// IME のコンポジション内のカーソル位置を取得します。
        /// </summary>
        public int Position { 
            get {
                return Lock(ctx =>
                {
                    var c = ImmGetCompositionString(ctx, GCS.CURSORPOS, null, 0) & 0xffff;
                    if (c == 0xffff) return -1;
                    return Encoding.Default.GetString(Encoding.Default.GetBytes(Composition), 0, c).Length;
                });
            }
        }

        /// <summary>
        /// コンポジションの文節のリストを取得します。
        /// </summary>
        /// <returns>文節の配列。</returns>
        public string[] GetCompositionClauses()
        {
            return Lock(ctx =>
            {
                var sz = ImmGetCompositionString(ctx, GCS.COMPCLAUSE, null, 0);
                var buf = new byte[sz];
                ImmGetCompositionString(ctx, GCS.COMPCLAUSE, buf, (uint)sz);
                if (sz == 0) return new string[0];
                var ofs = new int[sz / 4 - 1];
                using (var stream = new System.IO.MemoryStream(buf))
                using (var reader = new System.IO.BinaryReader(stream))
                {
                    for (int i = 0; i < sz / 4 - 1; i++)
                        ofs[i] = reader.ReadInt32();
                }

                var cmp = Composition;
                var bytes = Encoding.Default.GetBytes(cmp);
                var res = new string[ofs.Length];
                for (var i = 0; i < ofs.Length - 1; i++)
                    res[i] = Encoding.Default.GetString(bytes, ofs[i], ofs[i + 1] - ofs[i]);
                res[res.Length - 1] = Encoding.Default.GetString(bytes, ofs[res.Length - 1], bytes.Length - ofs[res.Length - 1]);
                return res;
            });
        }

        /// <summary>
        /// 指定した入力コンテキストを関連付けします。
        /// </summary>
        public void AssociateContext(IntPtr hIMC)
        {
            ImmAssociateContextEx(hWnd, hIMC, 0);
        }

        /// <summary>
        /// デフォルトの入力コンテキストと関連付けします。
        /// </summary>
        public void AssociateDefaultContext()
        {
            ImmAssociateContextEx(hWnd, IntPtr.Zero, IACE.DEFAULT);
        }

        /// <summary>
        /// IMM の入力コンテキストに関する操作を提供します。
        /// </summary>
        public class Context
        {
            IntPtr hWnd;
            internal Context(IntPtr hWnd)
            {
                this.hWnd = hWnd;
            }

            /// <summary>
            /// 指定した入力コンテキストを関連付けします。
            /// </summary>
            public void Associate(IntPtr hIMC)
            {
                ImmAssociateContextEx(hWnd, hIMC, 0);
            }

            /// <summary>
            /// デフォルトの入力コンテキストと関連付けします。
            /// </summary>
            public void AssociateDefaultContext()
            {
                ImmAssociateContextEx(hWnd, IntPtr.Zero, IACE.DEFAULT);
            }
        }

        /// 
        public class ContextArgs : EventArgs
        {
            /// <summary>
            /// 入力コンテキストに関する操作。
            /// </summary>
            public Context Context { get; private set; }

            /// <summary>
            /// イベントをハンドルしたかどうか。
            /// </summary>
            public bool Handled { get; set; }
            internal ContextArgs(Context context)
            {
                Context = context;
            }
        }

        /// <summary>
        /// ウィンドウに対して入力コンテキストの状態が変更された時に発生します。
        /// </summary>
        public event EventHandler<ContextArgs> SetContext;
        /// <summary>
        /// コンポジションが開始されたときに発生します。
        /// </summary>
        public event EventHandler StartComposition;
        /// <summary>
        /// コンポジションが終了された時に発生します。
        /// </summary>
        public event EventHandler EndComposition;
        /// <summary>
        /// コンポジションが変更された時に発生します。
        /// </summary>
        public event EventHandler CompositionChanged;
        /// <summary>
        /// 文字列を受け取った際に発生します。
        /// </summary>
        public event KeyPressEventHandler CharReceive;
        /// <summary>
        /// IME の ON/OFF が切り替わった際に発生します。
        /// </summary>
        public event EventHandler OpenStatusChanged;

        /// <summary>
        /// ウィンドウメッセージを処理します。
        /// </summary>
        /// <returns>デフォルのと処理を抑制するか否か。true が返った場合、デフォルトのウィンドウ プロシージャは実行されるべきではありません。</returns>
        public bool WndProc(ref Message m)
        {
            if (m.HWnd != hWnd) return false;
            switch (m.Msg)
            {
                case WM_IME_SETCONTEXT:
                    var arg = new ContextArgs(new Context(hWnd));
                    if (SetContext != null)
                        SetContext(this, arg);
                    return arg.Handled;
                case WM_IME_NOTIFY:
                    if (m.WParam.ToInt32() == (int)IMN.SETOPENSTATUS && OpenStatusChanged != null)
                        OpenStatusChanged(this, EventArgs.Empty);

                    break;
                case WM_IME_STARTCOMPOSITION:
                    if (StartComposition != null) StartComposition(this, EventArgs.Empty);
                    break;
                case WM_IME_ENDCOMPOSITION:
                    if (EndComposition != null) EndComposition(this, EventArgs.Empty);
                    break;
                case WM_IME_COMPOSITION:
                    if (CompositionChanged != null) CompositionChanged(this, EventArgs.Empty);

                    break;
                case WM_CHAR:
                    var ea = new KeyPressEventArgs((char)m.WParam.ToInt32());
                    if (CharReceive != null)
                        CharReceive(this, ea);
                    return ea.Handled;
            }
            return false;
        }

        /// <summary>
        /// 変換候補のリストとそれにまつわる情報を取得します。
        /// </summary>
        public CandidateList GetCandidateList()
        {
            return Lock(ctx =>
            {
                var sz = ImmGetCandidateList(ctx, 0, null, 0);
                var buf = new sbyte[sz];
                ImmGetCandidateList(ctx, 0, buf, (uint)sz);

                unsafe
                {
                    fixed (sbyte* p = buf)
                    {
                        CANDIDATELIST* cl = (CANDIDATELIST*)p;
                        var candidates = new string[cl->dwCount];
                        for (var i = 0; i < candidates.Length; i++)
                            candidates[i] = new string(p + cl->dwOffset[i]);
                        return new CandidateList(candidates, (int)cl->dwSelection, (int)cl->dwPageStart, (int)cl->dwPageSize);
                    }
                }
            });
        }

        unsafe struct CANDIDATELIST
        {
#pragma warning disable 649
            public uint dwSize;
            public uint dwStyle;
            public uint dwCount;
            public uint dwSelection;
            public uint dwPageStart;
            public uint dwPageSize;
#pragma warning restore 649
            public fixed uint dwOffset[1];

            private CANDIDATELIST Init()
            {
                return new CANDIDATELIST();
            }
        }

        /// <summary>
        /// 変換候補のリストとそれにまつわる情報を提供します。
        /// </summary>
        public class CandidateList : IEnumerable<string>
        {
            List<string> candidates;

            /// <summary>
            /// 現在選択されている変換候補。
            /// </summary>
            public int Selection { get; internal set; }

            /// <summary>
            /// ページの開始位置。
            /// </summary>
            public int PageStart { get; internal set; }

            /// <summary>
            /// ページ内のアイテムの数。
            /// </summary>
            public int PageSize { get; internal set; }

            internal CandidateList(IEnumerable<string> candidates, int selection, int pageStart, int pageSize)
            {
                this.candidates = candidates.ToList();
                Selection = selection;
                PageSize = pageSize;
                PageStart = pageStart;
            }

            /// <summary>
            /// <see cref="CandidateList"/> を反復処理する列挙しを返します。
            /// </summary>
            public IEnumerator<string> GetEnumerator()
            {
                return candidates.GetEnumerator();
            }

            /// <summary>
            /// インデックスの要素を取得します。
            /// </summary>
            /// <param name="index">インデックス</param>
            public string this[int index] { get { return candidates[index]; } }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }

}
