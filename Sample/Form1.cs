using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Arith.Input.Text;

namespace ImmSample
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            imm = new Imm(this);
            imm.SetContext += (x, y) =>
            {
                y.Handled = true;
                y.Context.AssociateDefaultContext();
            };
            imm.EndComposition += (x, y) =>
            {
                clauses = new string[0];
                pictureBox1.Refresh();
            };

            imm.CompositionChanged += (x, y) =>
            {
                clauses = imm.GetCompositionClauses();
                pictureBox1.Refresh();
            };

            imm.CharReceive += (x, y) =>
            {
                switch (y.KeyChar)
                {
                    case (char)Keys.Back:
                        if (confirmed.Length > 0)
                            confirmed = confirmed.Substring(0, confirmed.Length - 1);
                        break;
                    case (char)Keys.Escape:
                        confirmed = "";
                        break;
                    default:
                        confirmed += y.KeyChar;
                        break;
                }
                pictureBox1.Refresh();

                y.Handled = true;
            };

            imm.OpenStatusChanged += (x, y) => pictureBox1.Refresh();
        }

        string confirmed = "";
        string[] clauses = new string[0];

        Imm imm;

        protected override void WndProc(ref Message m)
        {
            if (!imm.WndProc(ref m))
                base.WndProc(ref m);
        }

        Brush brush;

        private void pictureBox1_Paint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            
            brush = brush ?? new SolidBrush(this.ForeColor);

            var sf = new StringFormat()
            {
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip
            };

            if (clauses.Length > 0)
            {
                var cr = new CharacterRange[clauses.Length];
                var ofs = 0;
                for (var i = 0; i < clauses.Length; i++)
                {
                    cr[i] = new CharacterRange(ofs, clauses[i].Length);
                    ofs += clauses[i].Length;
                }
                sf.SetMeasurableCharacterRanges(cr);

                var regions = e.Graphics.MeasureCharacterRanges(string.Join("", clauses), this.Font, new RectangleF(0, 0, 0, 0), sf);


                for (var i = 0; i < regions.Length; i++)
                {
                    if (cr[i].First == imm.Position)
                    {
                        e.Graphics.FillRectangle(Brushes.LightGray, Rectangle.Round(regions[i].GetBounds(e.Graphics)));
                    }
                    e.Graphics.DrawRectangle(Pens.Red, Rectangle.Round(regions[i].GetBounds(e.Graphics)));
                }
            }
            var cdl = imm.GetCandidateList();
            e.Graphics.DrawString(string.Join("", clauses) + "\n" + confirmed + "\n" + cdl.Selection + ":" + string.Join(", " , cdl.Skip(cdl.PageStart).Take(cdl.PageSize)) + "\n" + (imm.OpenStatus ? "On" : "Off") + "(" + imm.ConversionMode.ToString() + "): " + imm.Position + "\n" + imm.Description, this.Font, brush, 0, 0, sf);

            
        }
    }
}
