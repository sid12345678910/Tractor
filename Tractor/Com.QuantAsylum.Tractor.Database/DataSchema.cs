﻿using System;
using System.Collections.Generic;
using System.Data.Linq;
using System.Drawing;
using System.IO;
using System.Data.Linq.Mapping;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Com.QuantAsylum.Tractor.Database
{
    // See https://www.codeproject.com/Articles/43025/A-LINQ-Tutorial-Mapping-Tables-to-Objects
    // See https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql/linq/how-to-dynamically-create-a-database

    [Database]
    public class TestResultDatabase : DataContext
    {
        public Table<Test> Tests;
        public TestResultDatabase(string connection) : base(connection) { }

        /// <summary>
        /// Converts a bitmap to a byte array
        /// </summary>
        /// <param name="bmp"></param>
        /// <returns></returns>
        public static byte[] BmpToBytes(Bitmap bmp)
        {
            using (var stream = new MemoryStream())
            {
                bmp.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Converts a byte array to a bitmap
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Bitmap BytesToBmp(byte[] data)
        {
            Bitmap bmp;
            using (var ms = new MemoryStream(data))
            {
                bmp = new Bitmap(ms);
            }

            return bmp;
        }
    }

    [Table(Name = "Tests")]
    public class Test
    {
        /// <summary>
        /// Unique ID for the test. This is the key. It has nothing to do with
        /// the device serial number. This is autogenerated by the database.
        /// </summary>
        [Column(IsPrimaryKey = true, IsDbGenerated = true)]
        public int Id { get; set; }

        /// <summary>
        /// The serial number of the device
        /// </summary>
        [Column]
        public string SerialNumber;

        /// <summary>
        /// A test session might refer to a particular manufacturing run, or an engineering
        /// build version or a board version. It can be anything
        /// </summary>
        [Column]
        public string SessionName;

        /// <summary>
        /// This is the name of the test file that was loaded to test this particular product
        /// </summary>
        [Column]
        public string TestFile;

        /// <summary>
        /// This is the MD5 of the loaded test file. This allows versions to be tracked.
        /// </summary>
        [Column]
        public string TestFileMD5;

        /// <summary>
        /// The name of the particular test
        /// </summary>
        [Column]
        public string Name { get; set; }

        /// <summary>
        /// Time time of the particular test
        /// </summary>
        [Column]
        public DateTime Time { get; set; }

        /// <summary>
        /// Pass/Fail for this test
        /// </summary>
        [Column]
        public bool PassFail { get; set; }

        /// <summary>
        /// The result of the test. 
        /// </summary>
        [Column]
        public string ResultString { get; set; }

        /// <summary>
        /// The result of the test. 
        /// </summary>
        [Column]
        public float Result { get; set; }

        /// <summary>
        /// The limits of the test required for PASS
        /// </summary>
        [Column]
        public string TestLimits { get; set; }

        /// <summary>
        /// Any image, graph, etc associated with this test
        /// </summary>
        [Column(DbType = "varbinary(max)")]
        public byte[] ImageArray { get; set; }
    }
}
