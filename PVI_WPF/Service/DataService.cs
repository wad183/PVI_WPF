using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Data.Sqlite;

namespace PVI_WPF
{ 
    internal class DataService
    {
        private readonly string _connectString;      

        public string DbPath { get; }

        public DataService(string path)
        {
            string FolderPath = Path.Combine(AppContext.BaseDirectory, $"{path}");
            Directory.CreateDirectory(FolderPath);
            DbPath = Path.Combine(FolderPath,"pvi.db");
            _connectString = $"Data Source={DbPath}";    
            CreateTables();                              
        }
        private void CreateTables() //ImageResult PillResult
        {
            using SqliteConnection con = new SqliteConnection(_connectString);
            con.Open();
            using SqliteCommand cmd = con.CreateCommand();
            cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS ImageResult (
    Id           INTEGER PRIMARY KEY AUTOINCREMENT,
    FileName     TEXT    NOT NULL,
    ImagePath    TEXT    NOT NULL,
    Total        INTEGER NOT NULL,
    OkCount      INTEGER NOT NULL,
    NgCount      INTEGER NOT NULL,
    ReviewCount  INTEGER NOT NULL,
    ElapsedMs    INTEGER NOT NULL,
    ProfileName  TEXT,
    NoiseMinArea REAL, MinArea REAL, MaxArea REAL, MinAspect REAL, MinSaturation REAL,
    ManualTotal  INTEGER,
    ManualNg     INTEGER,
    DetectedAt   TEXT    NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_ImageResult_Path ON ImageResult(ImagePath, DetectedAt);

CREATE TABLE IF NOT EXISTS PillResult (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    ImageResultId INTEGER NOT NULL,
    PillIndex     INTEGER NOT NULL,
    Row REAL, Col REAL, AngleDeg REAL, Length REAL, Width REAL, Area REAL, MeanSaturation REAL,
    Verdict       TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_PillResult_Image ON PillResult(ImageResultId);
";
            cmd.ExecuteNonQuery();
        }
        public void Save(string fileName, string imagePath, List<PillResult> pills, DetectParams p, string? profileName, long elapsedMs)
        {
            int ok = 0;
            int ng = 0;
            int review = 0;
            foreach (PillResult i in pills)
            {
                if (i.IsIncomplete) review++;
                else if (i.IsOK) ok++;
                else ng++;
            }
            using SqliteConnection con = new SqliteConnection(_connectString);
            con.Open();

            using SqliteTransaction tx = con.BeginTransaction();

            long imageId;

            using (SqliteCommand cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"
                INSERT INTO ImageResult
                (FileName, ImagePath, Total, OkCount, NgCount, ReviewCount, ElapsedMs, ProfileName,
                NoiseMinArea, MinArea, MaxArea, MinAspect, MinSaturation, DetectedAt)
                VALUES
                ($fileName, $imagePath, $total, $ok, $ng, $review, $ms, $profile,
                $noise, $minArea, $maxArea, $minAspect, $minSat, $at);
                SELECT last_insert_rowid();
";

                cmd.Parameters.AddWithValue("$fileName", fileName);
                cmd.Parameters.AddWithValue("$imagePath", imagePath);
                cmd.Parameters.AddWithValue("$total", pills.Count);
                cmd.Parameters.AddWithValue("$ok", ok);
                cmd.Parameters.AddWithValue("$ng", ng);
                cmd.Parameters.AddWithValue("$review", review);
                cmd.Parameters.AddWithValue("$ms", elapsedMs);
                cmd.Parameters.AddWithValue("$profile", (object?)profileName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$noise", p.NoiseMinArea);
                cmd.Parameters.AddWithValue("$minArea", p.MinArea);
                cmd.Parameters.AddWithValue("$maxArea", p.MaxArea);
                cmd.Parameters.AddWithValue("$minAspect", p.MinAspect);
                cmd.Parameters.AddWithValue("$minSat", p.MinStaturation);
                cmd.Parameters.AddWithValue("$at", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

                imageId = (long)cmd.ExecuteScalar()!;
            }
            foreach (PillResult r in pills)
            {
                using SqliteCommand cmd = con.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = @"
INSERT INTO PillResult
  (ImageResultId, PillIndex, Row, Col, AngleDeg, Length, Width, Area, MeanSaturation, Verdict)
VALUES
  ($img, $idx, $row, $col, $angle, $len, $wid, $area, $sat, $verdict);";

                cmd.Parameters.AddWithValue("$img", imageId);
                cmd.Parameters.AddWithValue("$idx", r.index);
                cmd.Parameters.AddWithValue("$row", r.Row);
                cmd.Parameters.AddWithValue("$col", r.Col);
                cmd.Parameters.AddWithValue("$angle", r.AngleDeg);
                cmd.Parameters.AddWithValue("$len", r.Length);
                cmd.Parameters.AddWithValue("$wid", r.Width);
                cmd.Parameters.AddWithValue("$area", r.Area);
                cmd.Parameters.AddWithValue("$sat", r.MeanSaturation);
                cmd.Parameters.AddWithValue("$verdict", r.IsIncomplete ? "复检" : (r.IsOK ? "OK" : "NG"));

                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }
        public (int Images, int Pills) GetCounts()
        {
            using SqliteConnection conn = new SqliteConnection(_connectString);
            conn.Open();

            using SqliteCommand cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT (SELECT COUNT(*) FROM ImageResult), (SELECT COUNT(*) FROM PillResult);";
            using SqliteDataReader reader = cmd.ExecuteReader();
            if (!reader.Read()) return (0, 0);
            return (reader.GetInt32(0), reader.GetInt32(1));
        }

        // ★ 新增：查历史结果（每张图只取【最新一次】；onlyNg=true 只看有 NG 的图）
        public List<ImageRow> QueryImages(bool onlyNg)
        {
            var list = new List<ImageRow>();

            using SqliteConnection conn = new SqliteConnection(_connectString);
            conn.Open();
            using SqliteCommand cmd = conn.CreateCommand();
            // ★ 注意：判"最新一次"必须用 Id（自增主键），不能用 DetectedAt！
            //   同一秒内检测两次（翻页重检/连续跑）时间戳会一模一样 → 用时间判断会选出多行。
            cmd.CommandText = @"
SELECT Id, FileName, ImagePath, Total, OkCount, NgCount, ReviewCount, ElapsedMs,
       IFNULL(ProfileName,''), DetectedAt
FROM ImageResult i
WHERE i.Id = (SELECT MAX(Id) FROM ImageResult WHERE ImagePath = i.ImagePath)
  AND ($onlyNg = 0 OR i.NgCount > 0)
ORDER BY i.Id DESC;";

            // 不拼 SQL 字符串，用参数（$onlyNg=0 就相当于"不筛"）
            cmd.Parameters.AddWithValue("$onlyNg", onlyNg ? 1 : 0);

            using SqliteDataReader r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ImageRow
                {
                    Id = r.GetInt64(0),
                    FileName = r.GetString(1),
                    ImagePath = r.GetString(2),
                    Total = r.GetInt32(3),
                    OkCount = r.GetInt32(4),
                    NgCount = r.GetInt32(5),
                    ReviewCount = r.GetInt32(6),
                    ElapsedMs = r.GetInt64(7),
                    ProfileName = r.GetString(8),
                    DetectedAt = r.GetString(9),
                });
            }
            return list;
        }
    }
}
