using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ChartApp
{
    public partial class Login : Form
    {
        public Login()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (textBox1.Text != "" && textBox2.Text != "")//两个文本框都不为空
            {
                login();
            }
            else
            {
                MessageBox.Show("输入有空项，请重新输入");
            }
        }

        public void login()
        {
            //if (textBox1.Text == "1" && textBox2.Text == "1")
            //{
            //    Main main1 = new Main(true, 100);
            //    this.Hide();            //隐藏原窗体
            //    main1.ShowDialog();     //对话框窗体的设计
            //}
            //账号密码登录
            Dao dao = new Dao();

            string sql = $"select * from login_ur where id='{textBox1.Text}' and psw='{textBox2.Text}'";
            IDataReader dc = dao.read(sql);//逐行读取数据库select结果
            if (dc.Read())
            {
                Main main1 = new Main(true, 100);
                this.Hide();            //隐藏原窗体
                main1.ShowDialog();     //对话框窗体的设计
                this.Show();            //显示原窗体
            }
            else
            {
                MessageBox.Show("登录失败！", "错误");
            }
            dao.DaoClose();

        }

        private void button2_Click(object sender, EventArgs e)
        {
            Main main2 = new Main(false, 1, 250);
            this.Hide();            //隐藏原窗体
            //this.Close();
            main2.ShowDialog();     //对话框窗体的设计
            //main2.Close();
            //main2.Show();            //显示原窗体
        }

        private void Login_Load(object sender, EventArgs e)
        {

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
