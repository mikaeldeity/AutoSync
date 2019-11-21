using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;

namespace AutoSync
{
    public partial class DismissDialog : Form
    {
        public DismissDialog()
        {
            InitializeComponent();
        }

        private void DismissButton_Click(object sender, EventArgs e)
        {            
            DialogResult = DialogResult.OK;
            Close();
        }
        private void Dialogtimer_Tick(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
