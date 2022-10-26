using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Net;
using System.Windows.Forms;

namespace Yahoo_Para_Robot
{
    public partial class frmMain : Form
    {
        public tools myTools = new tools();
        public DAL myDal = new DAL();
        public string constr = "";
        string simdikitarih = "";
        ArrayList ParaListesi = new ArrayList();
        ArrayList IDListesi = new ArrayList();
        ArrayList Basarili = new ArrayList();
        public frmMain()
        {
            InitializeComponent();
        }
        private static double ConvertDateTimeToTimestamp(DateTime value)
        {
            TimeSpan epoch = (value - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToLocalTime());
            return (double)epoch.TotalSeconds;
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            myDal.frmMain = this;
            myTools.frmMain = this;
            myTools.logWriter("Robot Başladı");
            constr = System.Configuration.ConfigurationSettings.AppSettings["con"].ToString();
            myDal.OpenSQLConnection(constr, myDal.myConnection);
            var parca = ConvertDateTimeToTimestamp(DateTime.UtcNow).ToString().Split(',');
            simdikitarih = parca[0];
            DateTime dt = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 2, 0, 0);
            simdikitarih = ConvertDateTimeToTimestamp(dt).ToString().Split('.')[0];
            timer1.Interval = 3000;
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            button1_Click(null, null);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ParaListesiDoldur();
            ParaCek();
        }
        private void ParaListesiDoldur()
        {
            string sql = "select id, kisaltma from PARA where kisaltma != 'USD'";
            SqlDataReader oku = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
            while (oku.Read())
            {
                ParaListesi.Add(oku[1]);
                IDListesi.Add(oku[0]);
                myTools.logWriter("Para çekildi: " + oku[1].ToString());
                Application.DoEvents();
            }
        }
        private void ParaCek()
        {
            int sayac = 0;
            for (int i = 0; i < ParaListesi.Count; i++)
            {
                string starih = "";
                string sql = $"select top 1 tarih from DATA_GUNLUK_PARA where para_id={IDListesi[i]} order by tarih desc";
                SqlDataReader oku = myDal.CommandExecuteSQLReader(sql, myDal.myConnection);
                while (oku.Read())
                    starih = oku[0].ToString();
                if (starih == "")
                    if (!DataGetir(ParaListesi[i].ToString()).Contains("404 Not Found"))
                    {
                        VeriKaydet(DataGetir(ParaListesi[i].ToString()), Convert.ToInt32(IDListesi[i]), ParaListesi[i].ToString());
                        sayac++;
                    }
                    else
                        continue;
                else
                {
                    var parca = starih.Split('.');
                    parca[2] = parca[2].Substring(0, 4);
                    DateTime dt = new DateTime(Convert.ToInt32(parca[2]), Convert.ToInt32(parca[1]), Convert.ToInt32(parca[0]), 0, 0, 0);
                    starih = ConvertDateTimeToTimestamp(dt).ToString().Split('.')[0];
                    int starihi = Convert.ToInt32(starih) + 7200 + 86400;
                    if (starihi < Convert.ToInt32(simdikitarih))
                    {
                        if (!DataGetir(ParaListesi[i].ToString()).Contains("404 Not Found"))
                        {
                            VeriKaydet(DataGetir(starihi.ToString(), ParaListesi[i].ToString()), Convert.ToInt32(IDListesi[i]), ParaListesi[i].ToString());
                            sayac++;
                        }

                        else
                            continue;
                    }
                    else
                        continue;
                }
                Application.DoEvents();
            }
            myTools.logWriter("Para Çekimi Bitti. " + sayac + " tane paranın verileri çekildi.");
        }
        private string DataGetir(string para)
        {
            WebClient wc = new WebClient();
            try
            {
                return wc.DownloadString("https://query1.finance.yahoo.com/v7/finance/download/" + para + "USD=X?period1=1104710400&period2=" + simdikitarih + "&interval=1d&events=history");

            }
            catch (Exception)
            {
                return "404 Not Found";
            }
        }
        private string DataGetir(string tarih, string para)
        {
            WebClient wc = new WebClient();
            try
            {
                return wc.DownloadString("https://query1.finance.yahoo.com/v7/finance/download/" + para + "USD=X?period1=" + tarih + "&period2=" + simdikitarih + "&interval=1d&events=history");
            }
            catch (Exception)
            {
                return "404 Not Found";
            }
        }
        private void VeriKaydet(string veri, int id, string para)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("tarih", typeof(DateTime));
            dt.Columns.Add("para_id", typeof(int));
            dt.Columns.Add("fiyat", typeof(decimal));
            string[] satirlar = System.Text.RegularExpressions.Regex.Split(veri, "\n");
            for (int i = 1; i < satirlar.Length; i++)
            {
                string[] row = System.Text.RegularExpressions.Regex.Split(satirlar[i], ",");
                if (row[2] == "null") continue;
                DataRow dr = dt.NewRow();
                dr["tarih"] = row[0];
                dr["para_id"] = id;
                dr["fiyat"] = row[4].Replace(".",",");
                dt.Rows.Add(dr);
                Application.DoEvents();
            }
            BulkInsert(dt);
            myDal.CommandExecuteNonQuery($"update para set cekildi=1, kaynak='Yahoo Finance' where kisaltma = '{para}'", myDal.myConnection);
            myTools.logWriter("Para Eklendi: " + para);
        }
        private void BulkInsert(DataTable dt)
        {
            SqlConnection con = new SqlConnection(constr);
            SqlBulkCopy bulk = new SqlBulkCopy(con);
            bulk.DestinationTableName = "DATA_GUNLUK_PARA";
            con.Open();
            bulk.WriteToServer(dt);
            con.Close();
        }
    }
}
