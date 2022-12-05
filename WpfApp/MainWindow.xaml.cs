﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Sqlite;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System.ComponentModel.DataAnnotations;
using Microsoft.Win32;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
//using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Polly;
using System.Net.Http;
using Polly.Retry;
using System.Security.Cryptography;

namespace WpfApp
{
    class ImageContext : DbContext
    {
        public DbSet<Picture> Pictures { get; set; }
        public DbSet<PictureHash> PicturesHash { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder o)
        {
            o.UseSqlite("Data Source=DatabaseEm.db");
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static List<string> emotions_list = new() { "neutral", "happiness", "surprise", "sadness", "anger", "disgust", "fear", "contempt" };
        Dictionary<string, ObservableCollection<ImageInfo>> dict_all = new Dictionary<string, ObservableCollection<ImageInfo>>()
        {
            { "neutral", new ObservableCollection<ImageInfo>() },
            { "happiness", new ObservableCollection<ImageInfo>() },
            { "surprise", new ObservableCollection<ImageInfo>() },
            { "sadness", new ObservableCollection<ImageInfo>() },
            { "anger", new ObservableCollection<ImageInfo>() },
            { "disgust", new ObservableCollection<ImageInfo>() },
            { "fear", new ObservableCollection<ImageInfo>() },
            { "contempt", new ObservableCollection<ImageInfo>() },
            { "all", new ObservableCollection<ImageInfo>() },
            { "new", new ObservableCollection<ImageInfo>() }
        };

        Dictionary<string, ObservableCollection<ImageInfo>> dict_new = new Dictionary<string, ObservableCollection<ImageInfo>>()
        {
            { "neutral", new ObservableCollection<ImageInfo>() },
            { "happiness", new ObservableCollection<ImageInfo>() },
            { "surprise", new ObservableCollection<ImageInfo>() },
            { "sadness", new ObservableCollection<ImageInfo>() },
            { "anger", new ObservableCollection<ImageInfo>() },
            { "disgust", new ObservableCollection<ImageInfo>() },
            { "fear", new ObservableCollection<ImageInfo>() },
            { "contempt", new ObservableCollection<ImageInfo>() },
            { "all", new ObservableCollection<ImageInfo>() },
        };

        EmotionsLib.EmotionFerPlus EmotionCounter = new();
        CancellationTokenSource cts;
        bool TaskInProcess = false;

        // ----- task 4 -----
        // private AsyncRetryPolicy retryPolicy;
        // private int MAX_RETRIES = 3;
        // private string url = "http://localhost:5000/images";
        // ----------------------------

        static string ComputeSha256Hash(byte[] rawData)
        {  
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(rawData);
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            using (var db = new ImageContext())
            {
                if (db.Pictures.Any())
                {
                    //var imgs = db.Pictures.Where(a => a.Deleted.Equals(0));
                    var imgs = db.Pictures;
                    foreach (var img in imgs)
                    {
                        string file_name = img.Path;
                        BitmapImage imgBmi = new BitmapImage(new Uri(file_name));
                        string info = img.Info;
                        //string short_name = file_name.Remove(0, file_name.LastIndexOf('\\') + 1);
                        var tmp = new ImageInfo(file_name, imgBmi, info, img.Section);
                        dict_all["all"].Add(tmp);
                        dict_all[img.Section].Add(tmp);
                    }
                }
            }
            DataContext = dict_all;
            //retryPolicy = Policy.Handle<HttpRequestException>().WaitAndRetryAsync(MAX_RETRIES, times => 
                   // TimeSpan.FromMilliseconds(times * 500));
        }

        private void LoadImagesCmd(object sender, RoutedEventArgs e)
        {
            var fbd = new Ookii.Dialogs.Wpf.VistaFolderBrowserDialog();
            //var ofd = new FolderBrowserDialog();
            //ofd.Filter = "Image Files(*.BMP;*.JPG;*.GIF;*.PNG)|*.BMP;*.JPG;*.GIF;*.PNG";
            //fbd.Multiselect = true;
            string path;
            string[] file_names;

            if (fbd.ShowDialog() == true)
            {
                path = fbd.SelectedPath;
                file_names = Directory.GetFiles(path);
            }
            else return;

            foreach (string file_name in file_names)
            {
                BitmapImage imgBmi = new BitmapImage(new Uri(file_name));
                //string short_name = file_name.Remove(0, file_name.LastIndexOf('\\') + 1);
                dict_all["new"].Add(new ImageInfo(file_name, imgBmi, "", ""));
            }
            listNew.ItemsSource = dict_all["new"];
        }

        private async Task<string> AnalyzeImage(ImageInfo image, CancellationTokenSource cts)
        {
            var Emotions = await Task.Run(async () =>
            {
                Image<Rgb24> img_rgb = SixLabors.ImageSharp.Image.Load<Rgb24>(image.FilePath);
                var emotions = EmotionCounter.EmotionRecognition(img_rgb, cts.Token).Result;
                emotions.Sort((a, b) => -(a.Item2.CompareTo(b.Item2)));
                image.AssignInfo(emotions);
                return emotions;
            }, cts.Token);
            string field = Emotions[0].Item1;
            image.Section = field;
            dict_new[field].Add(image);
            dict_new["all"].Add(image);

            return field;
        }

        private void CancelCmd(object sender, RoutedEventArgs e)
        {
            if (!TaskInProcess)
            {
                MessageBox.Show("Analysis wasn't started");
            }
            else
            {
                cts.Cancel();
                foreach (var pair in dict_all)
                {
                    pair.Value.Clear();
                }

                progress_bar.Visibility = Visibility.Hidden;
                txt_progress.Visibility = Visibility.Visible;

                TaskInProcess = false;
            }
        }

        //private void ClearCmd(object sender, RoutedEventArgs e)
        //{
        //    if (((TabItem)TabCtrl.SelectedItem).HasHeader)
        //    {
        //        var i = ((TabItem)TabCtrl.SelectedItem).Header.ToString().ToLower();

        //        dict_all[i].Clear();
        //    } else
        //    {
        //        MessageBox.Show($"Header not found in this tab");
        //        var ind = TabCtrl.SelectedIndex;
        //        dict_all[emotions_list[ind]].Clear();
        //    }
        //}

        private void ClearAllCmd(object sender, RoutedEventArgs e)
        {
            using (var db = new ImageContext())
            {
                if (db.Pictures.Any()) {
                    foreach (var pic in db.Pictures)
                    {
                        db.Pictures.Remove(pic);
                    }
                    MessageBox.Show("DB has been emptied.");
                }
                if (db.PicturesHash.Any())
                {
                    foreach (var pic in db.PicturesHash)
                    {
                        db.PicturesHash.Remove(pic);
                    }
                    MessageBox.Show("DB has been emptied.");
                }
                
                db.SaveChanges();
            }
            foreach (var pair in dict_all)
            {
                pair.Value.Clear();
            }
        }

        private async void AnalyzeImagesCmd(object sender, RoutedEventArgs e)
        {
            if (dict_all["new"].Count == 0)
            {
                MessageBox.Show("No images to analyze");
            }
            else
            {
                TaskInProcess = true;
                mi_analyze.IsEnabled = false;
                mi_load.IsEnabled = false;

                try
                {

                    progress_bar.Visibility = Visibility.Visible;
                    progress_bar.Maximum = dict_all["new"].Count;
                    progress_bar.Value = 0;
                    var step = progress_bar.Maximum / dict_all["new"].Count;
                    txt_progress.Visibility = Visibility.Hidden;

                    cts = new CancellationTokenSource();

                    using (var db = new ImageContext())
                    {
                        foreach (var image in dict_all["new"])
                        {
                            var img_byte = File.ReadAllBytes(image.FilePath);
                            var img_hash = ComputeSha256Hash(img_byte);
                            var img_right_hash = db.PicturesHash.Where(i => i.Hash.Equals(img_hash));
                            MessageBox.Show($"{img_right_hash.Count()}"); 


                            if (img_right_hash.Count() == 0)
                            {
                                //var img_right_image = db.PicturesHash.Where(i => i.Image.Equals(img_byte));
                                //MessageBox.Show($"{img_right_image.Count()}");

                                //if (img_right_image.Count() == 0)
                                //{
                                    string section = await AnalyzeImage(image, cts);
                                    Picture newPic = new Picture
                                    {
                                        Path = image.FilePath,
                                        Section = section,
                                        Info = image.Info
                                    };
                                    db.Pictures.Add(newPic);
                                    db.SaveChanges();

                                    var newId = db.Pictures.OrderBy(i => i.ImageId).Last().ImageId;
                                    //MessageBox.Show($"new Id {newId}");
                                    PictureHash newPicHash = new PictureHash
                                    {
                                        Hash = img_hash,
                                        Image = img_byte,
                                        ImageId = newId
                                    };

                                    db.PicturesHash.Add(newPicHash);
                                    db.SaveChanges();
                                //}
                            }
                            else
                            {
                                var img_right_image = img_right_hash.Where(i => i.Image.Equals(img_byte));

                                if (img_right_image.Count() == 0)
                                {
                                    string section = await AnalyzeImage(image, cts);
                                    Picture newPic = new Picture
                                    {
                                        Path = image.FilePath,
                                        //Image = img_byte,
                                        Section = section,
                                        Info = image.Info
                                    };
                                    db.Pictures.Add(newPic);
                                    db.SaveChanges();
                                    var newId = db.Pictures.OrderBy(i => i.ImageId).Last().ImageId;
                                        //Where(i => i.Equals(newPic)).Select(i => i.ImageId).FirstOrDefault();

                                    PictureHash newPicHash = new PictureHash
                                    {
                                        Hash = img_hash,
                                        Image = img_byte,
                                        ImageId = newId
                                    };

                                    db.PicturesHash.Add(newPicHash);
                                    db.SaveChanges();
                                }
                            }
                            progress_bar.Value += step;
                        }

                        progress_bar.Visibility = Visibility.Hidden;
                        txt_progress.Visibility = Visibility.Visible;

                        foreach (var state in dict_new.Keys)
                        {
                            foreach (var item in dict_new[state])
                            {
                                dict_all[state].Add(new ImageInfo(item));
                            }
                        }

                        foreach (var val in dict_new.Values)
                        {
                            val.Clear();
                        }

                        dict_all["new"].Clear();
                    }

                    listAll.ItemsSource = dict_all["all"];
                    listNeutral.ItemsSource = dict_all["neutral"];
                    listHappiness.ItemsSource = dict_all["happiness"];
                    listSurprise.ItemsSource = dict_all["surprise"];
                    listSadness.ItemsSource = dict_all["sadness"];
                    listAnger.ItemsSource = dict_all["anger"];
                    listDisgust.ItemsSource = dict_all["disgust"];
                    listFear.ItemsSource = dict_all["fear"];
                    listContempt.ItemsSource = dict_all["contempt"];
                }
                catch
                {
                    MessageBox.Show("Analysis was canceled");
                }

                TaskInProcess = false;
                mi_load.IsEnabled = true;
                mi_analyze.IsEnabled = true;
            }
        }


        private void DeleteImgCmd(object sender, RoutedEventArgs e)
        {
            string section = "list" + ((TabItem)TabCtrl.SelectedItem).Header.ToString();
            var lv_curr = TabCtrl.FindName(section) as ListView;
            var img_ii = lv_curr.SelectedItem as ImageInfo;
            
            if (img_ii.Section == "new")
            {
                dict_all["new"].Remove(img_ii);
            }
            else{
                dict_all[img_ii.Section].Remove(img_ii);
                dict_all["all"].Remove(img_ii);

                using (var db = new ImageContext())
                {
                    var image_delete = db.Pictures.Where(i => i.Path.Equals(img_ii.FilePath)).FirstOrDefault();
                    var image_hash_delete = db.PicturesHash.Where(i => i.ImageId.Equals(image_delete.ImageId)).FirstOrDefault();
                    db.Pictures.Remove(image_delete);
                    db.PicturesHash.Remove(image_hash_delete);
                    db.SaveChanges();
                }

                MessageBox.Show("Deleted from db");
            }
        }
    }
}
