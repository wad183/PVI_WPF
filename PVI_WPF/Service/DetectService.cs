using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;    
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Text.Json;

namespace PVI_WPF
{
    internal class DetectService
    {
        #region 文件管理
        private readonly string _folder;

        public DetectService()
        {
            _folder = ResolveFolder();   
        }

        /// <summary>依次尝试 AppData → exe 目录 → 临时目录，返回第一个能写的</summary>
        private static string ResolveFolder()
        {
            string[] candidates =
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PVI_WPF", "profiles"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles"),
                Path.Combine(Path.GetTempPath(), "PVI_WPF", "profiles"),
            };

            foreach (string dir in candidates)
            {
                try
                {
                    Directory.CreateDirectory(dir);
                    return dir;
                }
                catch
                {
                    // 这个位置没权限（或被安全软件挡住），换下一个
                }
            }
            return candidates[candidates.Length - 1];
        }
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { WriteIndented = true };
        public string Floder => _folder;

        //读取展示 添加 删除 加载
        public List<paramProfile> LoadAll() 
        {
            var list = new List<paramProfile>();
            if (!Directory.Exists(_folder)) return list;   // 目录建不出来时不要崩

            foreach (string file in Directory.GetFiles(_folder, "*.json"))
            {
                try
                {
                    paramProfile? profile = JsonSerializer.Deserialize<paramProfile>(File.ReadAllText(file));

                    if (string.IsNullOrEmpty(profile.Name))
                        profile.Name = Path.GetFileNameWithoutExtension(file);
                    list.Add(profile);
                }
                catch (Exception)
                {
                    //读取配置文件json失败
                }
            }
            return list.OrderBy(i => i.Name).ToList();
        }

        public void Save(string name,DetectParams p)
        {
            var profile = new paramProfile() { Name = name, detectParams = p };

            string json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
            {
                WriteIndented = true 
            });

            string path = Path.Combine(_folder, name + ".json");
            try
            {
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"配置保存失败：{ex.Message} 位置：{path}", "错误");
            }
        }

        public DetectParams? Load(string name)
        {
            string file = Path.Combine(_folder,name + ".json");
            if (!Path.Exists(file)) return null;
            try
            {
                string json = File.ReadAllText(file);
                return JsonSerializer.Deserialize<DetectParams>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Delete(string name)
        {
            string file = Path.Combine(_folder, name + ".json");
            if (File.Exists(file)) File.Delete(file);
        }


        #endregion

        public List<PillResult> Detect(string imagePath, DetectParams p)
        {
            List<PillResult> results = new List<PillResult>();
            //读图
            HImage image = new HImage(imagePath);
            HOperatorSet.GetImageSize(image, out HTuple width, out HTuple height);
            HOperatorSet.CountChannels(image, out HTuple channels);

            //通道
            HObject processImage; //二值化
            HObject? satImage = null;     //饱和度

            if (channels.I >= 3)
            {
                HOperatorSet.Decompose3(image, out HObject chR, out HObject chG, out HObject chB);
                HOperatorSet.TransFromRgb(chR, chG, chB, out HObject hue, out HObject sat, out HObject val, "hsv");
                satImage = sat;
                hue.Dispose();
                val.Dispose();
                chR.Dispose();
                chG.Dispose();
                chB.Dispose();
                if (p.Channel == "饱和度")
                {
                    HOperatorSet.CopyImage(sat, out processImage);
                }
                else
                {
                    HOperatorSet.Rgb1ToGray(image, out processImage);
                }
            }
            else
            {
                HOperatorSet.CopyImage(image, out processImage);
            }

            //二值化
            HObject region;
            if (p.ThresholdMethod == "固定阈值")
            {
                HOperatorSet.Threshold(processImage, out region, p.FixedThreshold, 255);
            }
            else
            {
                HOperatorSet.BinaryThreshold(processImage, out region, "max_separability"
                    , p.LightOrDark, out HTuple _);
            }

            //膨胀 拆解
            if (p.FillHoles)
            {
                HOperatorSet.FillUp(region, out HObject filled);
                region.Dispose();
                region = filled;
            }

            HOperatorSet.Connection(region, out HObject connected);
            region.Dispose();

            //去除噪点
            HOperatorSet.SelectShape(connected, out HObject parts, "area", "and", p.NoiseMinArea, 1.0e9);
            connected.Dispose();
            HOperatorSet.CountObj(parts, out HTuple partCount);
            if (partCount.I == 0)
            {
                parts.Dispose();
                satImage?.Dispose();
                processImage.Dispose();
                image.Dispose();
                return results;
            }

            HOperatorSet.SortRegion(parts, out HObject sorted, "first_point", "true", "row");
            parts.Dispose();

            //计算测量值
            HOperatorSet.CountObj(sorted, out HTuple count);

            if (count.I > 0)
            {
                HOperatorSet.AreaCenter(sorted, out HTuple areas, out HTuple rows, out HTuple cols);
                HOperatorSet.SmallestRectangle1(sorted, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
                HOperatorSet.SmallestRectangle2(sorted, out HTuple _, out HTuple _, out HTuple phis,
                                                out HTuple len1, out HTuple len2);
                HTuple satMeans = new HTuple();
                bool hasSat = false;
                if (satImage != null)
                {
                    HOperatorSet.Intensity(sorted, satImage, out satMeans, out HTuple _);
                    hasSat = true;
                }

                for (int i = 0; i < count.I; i++)
                {
                    // smallest_rectangle2 给的是**半长**，而且 len1 不一定比 len2 长，所以要自己取大小
                    double half1 = len1[i].D;
                    double half2 = len2[i].D;

                    double longHalf, shortHalf, longAngleRad;
                    if (half1 >= half2)
                    {
                        longHalf = half1; shortHalf = half2; longAngleRad = phis[i].D;
                    }
                    else
                    {
                        longHalf = half2; shortHalf = half1; longAngleRad = phis[i].D + Math.PI / 2.0;
                    }

                    double aspect = longHalf / Math.Max(1e-6, shortHalf);
                    double meanSat = hasSat ? satMeans[i].D : 0;

                    // 触边判定：外接矩形碰到图像边缘 margin 像素以内
                    bool incomplete = r1[i].D <= p.BorderMargin || c1[i].D <= p.BorderMargin
                                   || r2[i].D >= height.I - 1 - p.BorderMargin
                                   || c2[i].D >= width.I - 1 - p.BorderMargin;

                    // 判定：先看触边（半颗药既不算 OK 也不算 NG），再看三条特征
                    bool ok = !incomplete
                              && areas[i].D >= p.MinArea
                              && areas[i].D <= p.MaxArea
                              && aspect >= p.MinAspect
                              && (!hasSat || meanSat >= p.MinStaturation);

                    results.Add(new PillResult
                    {
                        index = i + 1,
                        Row = rows[i].D,
                        Col = cols[i].D,
                        AngleDeg = NormalizeAngle(longAngleRad * 180.0 / Math.PI),
                        Length = longHalf * 2.0,
                        Width = shortHalf * 2.0,
                        Area = areas[i].D,
                        MeanSaturation = meanSat,
                        IsOK = ok,
                        IsIncomplete = incomplete,
                    });
                }
            }
            //释放资源
            sorted.Dispose();
            satImage?.Dispose();
            processImage.Dispose();
            image.Dispose();
            return results;
        }
        private static double NormalizeAngle(double deg)
        {
            while (deg < -90) deg += 180;
            while (deg >= 90) deg -= 180;
            return deg;
        }
    }
}
