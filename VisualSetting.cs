using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NeuroBase
{
    public partial class VisualSetting : Form
    {
        
        int[] ChannelOn;
        public int[] _ChannelOn
        { get { return ChannelOn; } }
        public VisualSetting(int[] Channel)
        {
            InitializeComponent();
            
            ChannelOn = Channel;
            for (int iChannel = 0; iChannel < ChannelOn.Count(); iChannel++)
            {
                checkedListBox1.SetItemChecked(ChannelOn[iChannel], true);

            }
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            List<int> ChannelArray = new List<int>();
            for (int i=0;i< checkedListBox1.Items.Count;i++)
            {
                if(checkedListBox1.GetItemChecked(i))
                {
                    ChannelArray.Add(i);
                }
            }
            ChannelOn =new int[ChannelArray.Count()];
            ChannelArray.CopyTo(ChannelOn);
            
            //mainform.TimeOnPlot = 0;
            Close();
        }
    }
}
