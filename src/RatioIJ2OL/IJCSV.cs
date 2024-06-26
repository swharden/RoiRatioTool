﻿using System.Data;

namespace RatioIJ2OL;

public class IJCSV
{
    /// <summary>
    /// Total number of images across all channels/sections/times/series
    /// </summary>
    public int FrameCount { get; }

    /// <summary>
    /// Number of frames assuming every frame has paired red and green channels
    /// </summary>
    public int RatioFrameCount => FrameCount / ChannelCount;

    public const int ChannelCount = 2;

    public int RoiCount { get; }

    public double[,] Values { get; }

    public int SweepCount { get; set; } = 1;
    public int RoiFrameCountPerSweep => RatioFrameCount / SweepCount;
    public double FramePeriod { get; } = 0.067;
    public double BaselineTime1 { get; set; } = 0;
    public double BaselineTime2 { get; set; } = 1;
    public int BaselineIndex1 => (int)(BaselineTime1 / FramePeriod);
    public int BaselineIndex2 => (int)(BaselineTime2 / FramePeriod);
    public string FilePath { get; }
    public string Filename => Path.GetFileName(FilePath);

    /// <summary>
    /// Analyze an ImageJ ROI multi-measure CSV file.
    /// </summary>
    public IJCSV(string path)
    {
        FilePath = path;
        string[] lines = File.ReadAllLines(path);

        // skip header
        lines = lines[1..];

        FrameCount = lines.Length;
        RoiCount = lines[0].Split(",").Length - 1;
        Values = new double[FrameCount, RoiCount];

        for (int i = 0; i < FrameCount; i++)
        {
            double[] lineValues = lines[i].Split(",").Skip(1).Select(double.Parse).ToArray();
            for (int j = 0; j < RoiCount; j++)
            {
                Values[i, j] = lineValues[j];
            }
        }
    }

    public double GetFrameValue(int frame, int roi)
    {
        return Values[frame, roi];
    }

    public double GetRatioValue(int ratioFrame, int roi)
    {
        double r = Values[ratioFrame * 2, roi];
        double g = Values[ratioFrame * 2 + 1, roi];
        return g / r;
    }

    public double[] GetRatioAll(int roi)
    {
        double[] ratioValues = new double[RatioFrameCount];
        for (int i = 0; i < ratioValues.Length; i++)
        {
            ratioValues[i] = GetRatioValue(i, roi);
        }
        return ratioValues;
    }

    public double[] GetRatioSweep(int roi, int sweep)
    {
        double[] values = new double[RoiFrameCountPerSweep];

        for (int i = 0; i < values.Length; i++)
        {
            int frameIndex = i + sweep * RoiFrameCountPerSweep;
            values[i] = GetRatioValue(frameIndex, roi);
        }

        return values;
    }

    public double[] GetDffSweep(int roi, int sweep)
    {
        double[] values = GetRatioSweep(roi, sweep);

        double baseline = (BaselineIndex1 >= BaselineIndex2)
                ? values[BaselineIndex2]
                : values[BaselineIndex1..BaselineIndex2].Average();

        for (int i = 0; i < values.Length; i++)
        {
            double delta = values[i] - baseline;
            values[i] = delta / baseline * 100.0;
        }
        return values;
    }

    public double[,] GetDffChart(int roi)
    {
        double[,] values = new double[RoiFrameCountPerSweep, SweepCount];

        for (int sweep = 0; sweep < SweepCount; sweep++)
        {
            double[] dffSweep = GetDffSweep(roi, sweep);
            for (int frame = 0; frame < RoiFrameCountPerSweep; frame++)
            {
                values[frame, sweep] = dffSweep[frame];
            }
        }

        return values;
    }

    public DataTable GetDffDataTable(int roi)
    {
        double[,] values = GetDffChart(roi);
        DataTable dataTable = new();

        dataTable.Columns.Add("Time", typeof(float));
        for (int x = 0; x < values.GetLength(1); x++)
        {
            dataTable.Columns.Add($"Sweep {x + 1}", typeof(float));
        }

        for (int frame = 0; frame < values.GetLength(0); frame++)
        {
            DataRow dataRow = dataTable.NewRow();
            dataRow.SetField(0, frame * FramePeriod);

            for (int sweep = 0; sweep < values.GetLength(1); sweep++)
            {
                dataRow.SetField(sweep + 1, values[frame, sweep]);
            }
            dataTable.Rows.Add(dataRow);
        }

        return dataTable;
    }
}
