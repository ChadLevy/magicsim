﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Serialization;
using static magicsim.SimQueue;
using System.Globalization;
using System.Threading;
using System.Windows;
using System.Text.RegularExpressions;
using System.Windows.Media.Media3D;
using System.Windows.Media;
using HelixToolkit.Wpf;

namespace magicsim
{
    public enum ColorCoding
    {
        /// <summary>
        /// No color coding, use coloured lights
        /// </summary>
        ByLights,

        /// <summary>
        /// Color code by gradient in y-direction using a gradient brush with white ambient light
        /// </summary>
        ByGradientY
    }

    public class ResultsData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler RunningFailed;

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }

        private List<Tuple<SimResult, string>> _results;
        private List<Tuple<string, string>> _reforgeResults;

        public ObservableCollection<PlayerResult> MergedResults { get; set; }

        private PlayerResult _selectedPlayer;
        public PlayerResult SelectedPlayer
        {
            get { return _selectedPlayer; }
            set
            {
                if (value != _selectedPlayer)
                {
                    _selectedPlayer = value;
                    PawnString = GetPawnString(value);
                    SummaryString = GetSummaryString(value);
                    OnPropertyChanged("SelectedPlayer");
                }
            }
        }

        private string _pawnString;
        public string PawnString
        {
            get { return _pawnString; }
            set
            {
                if (value != _pawnString)
                {
                    _pawnString = value;
                    OnPropertyChanged("PawnString");
                }
            }
        }

        private string _labelString;
        public string LabelString
        {
            get { return _labelString; }
            set
            {
                if (value != _labelString)
                {
                    _labelString = value;
                    OnPropertyChanged("LabelString");
                }
            }
        }

        private string _summaryString;
        public string SummaryString
        {
            get { return _summaryString; }
            set
            {
                if (value != _summaryString)
                {
                    _summaryString = value;
                    OnPropertyChanged("SummaryString");
                }
            }
        }

        private Dictionary<string, double> playerDpsValues;
        private Dictionary<string, double> playerDamageValues;
        private Dictionary<string, double> playerMainStatValues;
        private Dictionary<string, string> playerMainStatTypes;
        private Dictionary<string, double> playerHasteValues;
        private Dictionary<string, double> playerCritValues;
        private Dictionary<string, double> playerMasteryValues;
        private Dictionary<string, double> playerVersValues;
        private Dictionary<string, string> playerSpecs;
        private Dictionary<string, string> playerClasses;

        // Maps player name.
        private Dictionary<string, PlayerReforge> reforges;
        private Dictionary<string, Player> players;

        public ObservableCollection<PlayerReforge> MergedReforges;

        private string _modelName;
        public string ModelName
        {
            get { return _modelName; }
            set
            {
                if (value != _modelName)
                {
                    _modelName = value;
                    LabelString = GetLabelString();
                    OnPropertyChanged("ModelName");
                }
            }
        }

        private string _modelNameShort;
        public string ModelNameShort
        {
            get { return _modelNameShort; }
            set
            {
                if (value != _modelNameShort)
                {
                    _modelNameShort = value;
                    OnPropertyChanged("ModelNameShort");
                }
            }
        }

        private string _tag;
        public string Tag
        {
            get { return _tag; }
            set
            {
                if (value != _tag)
                {
                    _tag = value;
                    LabelString = GetLabelString();
                    OnPropertyChanged("Tag");
                }
            }
        }

        public ObservableCollection<Tuple<string,Point3D[,],double[,]>> NamedDataColorValueSets { get; set; }

        public double[,] ColorValues { get; set; }

        public ColorCoding ColorCoding { get; set; }

        public Model3DGroup Lights
        {
            get
            {
                var group = new Model3DGroup();
                switch (ColorCoding)
                {
                    case ColorCoding.ByGradientY:
                        group.Children.Add(new AmbientLight(Colors.White));
                        break;
                    case ColorCoding.ByLights:
                        group.Children.Add(new AmbientLight(Colors.Gray));
                        group.Children.Add(new PointLight(Colors.Red, new Point3D(0, -1000, 0)));
                        group.Children.Add(new PointLight(Colors.Blue, new Point3D(0, 0, 1000)));
                        group.Children.Add(new PointLight(Colors.Green, new Point3D(1000, 1000, 0)));
                        break;
                }
                return group;
            }
        }

        public Brush SurfaceBrush
        {
            get
            {
                // Brush = BrushHelper.CreateGradientBrush(Colors.White, Colors.Blue);
                // Brush = GradientBrushes.RainbowStripes;
                // Brush = GradientBrushes.BlueWhiteRed;
                switch (ColorCoding)
                {
                    case ColorCoding.ByGradientY:
                        return BrushHelper.CreateGradientBrush(Colors.Red, Colors.White, Colors.Blue);
                    case ColorCoding.ByLights:
                        return Brushes.White;
                }
                return null;
            }
        }

        public ResultsData()
        {
            _results = new List<Tuple<SimResult,string>>();
            _reforgeResults = new List<Tuple<string, string>>();
            MergedResults = new ObservableCollection<PlayerResult>();
            MergedReforges = new ObservableCollection<PlayerReforge>();
            playerCritValues = new Dictionary<string, double>();
            playerDpsValues = new Dictionary<string, double>();
            playerDamageValues = new Dictionary<string, double>();
            playerHasteValues = new Dictionary<string, double>();
            playerMainStatValues = new Dictionary<string, double>();
            playerMainStatTypes = new Dictionary<string, string>();
            playerMasteryValues = new Dictionary<string, double>();
            playerVersValues = new Dictionary<string, double>();
            playerSpecs = new Dictionary<string, string>();
            playerClasses = new Dictionary<string, string>();
            reforges = new Dictionary<string, PlayerReforge>();
            players = new Dictionary<string, Player>();
            NamedDataColorValueSets = new ObservableCollection<Tuple<string, Point3D[,], double[,]>>();
        }

        public string GetLabelString()
        {
            return "Results - " + Tag + " - " + ModelName;
        }

        public void LoadResultPath(String path)
        {
            SimDataManager.ResetSimData();
            var results = Directory.EnumerateFiles(path, "*.json");
            _results.Clear();
            results.ToList().ForEach((result) =>
            {
                try
                {
                    using (StreamReader r = new StreamReader(result))
                    {
                        string json = r.ReadToEnd();

                        SimResult res = JsonConvert.DeserializeObject<SimResult>(json);
                        _results.Add(new Tuple<SimResult, string>(res, result.Split(Path.DirectorySeparatorChar).Last()));
                    }
                } catch(Exception e)
                {
                    MessageBox.Show("Could not process generated results. Something went terribly wrong.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    RunningFailed(this, new EventArgs());
                }
            });
            var reforgeResults = Directory.EnumerateFiles(path, "*.csv");
            reforgeResults.ToList().ForEach((reforgeResult) =>
            {
                try
                {
                     _reforgeResults.Add(new Tuple<string, string>(File.ReadAllText(reforgeResult), reforgeResult.Split(Path.DirectorySeparatorChar).Last()));
                }
                catch (Exception e)
                {
                    MessageBox.Show("Could not process generated results. Something went terribly wrong.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    RunningFailed(this, new EventArgs());
                }
            });
        }

        public Point3D[,] CreateDataMatrixFromPoints(List<Point3D> points)
        {
            List<List<Point3D>> associativeMatrix = new List<List<Point3D>>();
            List<Point3D> unsearchedSpace = points.OrderBy(x => x.X).ThenBy(x => x.Y).ToList();
            List<Point3D> unsearchedSubspace = null;
            Point3D? previous = null;
            while(unsearchedSpace.Count > 0)
            {
                Point3D cursor = new Point3D();
                if (!previous.HasValue)
                {
                    // First of this row. Instantiate a row and pop off the smallest x in the unsearchedSpace and have that parent the new row.
                    associativeMatrix.Add(new List<Point3D>());
                    cursor = unsearchedSpace[0];
                    // The subspace for this row is currently equal to the search space minus the selected cursor, ordered by y ascending to greedily find nearest neighbor.
                    unsearchedSubspace = unsearchedSpace.Skip(1).OrderBy(x => x.Y).ToList();
                }
                else
                {
                    if (unsearchedSubspace.Count == 0)
                    {
                        // Nothing left in the searchspace. Null out and start a new row.
                        previous = null;
                        continue;
                    }
                    try
                    {
                        // Filter out all the nodes that we believe are in a different row. IE: they are farther to the x-pos direction than they are to the y-pos direction. And take the one with the smallest Y distance to us (closest-in-row).
                        cursor = unsearchedSubspace.Where(x => x.Y - previous.Value.Y > x.X - previous.Value.X).OrderBy(x => x.Y - previous.Value.Y).First();
                        // Cut out all nodes that are disencluded from the row (since cursor was the closest we found valid, anything closer than cursor is no longer valid for the row).
                        // We also skip cursor obviously because it will be added the to searched space at the end of the loop.
                        // Note: We don't cut out all nodes that were too far away because our row may 'drift' in a direction over time and we don't want to filter out nodes too aggresively that may actually be in our row.
                        // We can optimize this heuristic in the future.
                        unsearchedSubspace = unsearchedSubspace.SkipWhile(x => !x.Equals(cursor)).Skip(1).ToList();
                    }
                    catch (Exception)
                    {
                        // Couldn't find a valid entry in the subspace. Null out so we start a new row.
                        previous = null;
                        continue;
                    }
                }

                // A node added to the matrix is considered 'searched'.
                // The matrix is only complete when all nodes are 'searched'
                unsearchedSpace.Remove(cursor);
                associativeMatrix.Last().Add(cursor);
                // To hint where to look for the next node, store reference to the current row tail.
                previous = cursor;
            }
            // Convert associativeMatrix into a multidimensional array. Currently a naive implementation assuming they are front-lined up and get back-padded.
            var columns = associativeMatrix.Max(x => x.Count);
            var rows = associativeMatrix.Count;
            var matrix = new Point3D[rows, columns];
            for(int i=0; i<rows; i++)
            {
                var row = associativeMatrix[i];
                for(int j=0; j<columns; j++)
                {
                    if(j >= row.Count)
                    {
                        matrix[i, j] = new Point3D(0.0,0.0,0.0);
                    } else
                    {
                        matrix[i, j] = row[j];
                    }
                }
            }
            return matrix;
        }

        public Point3D[,] CreateDataArray(PlayerReforge reforgeData)
        {
            // calculate MinX,MaxX, MinY,MaxY
            // columns*rows should equal total reforges
            var data = new Point3D[0, 0];
            var pointList = new List<Point3D>();
            GearResults gear = players[reforgeData.PlayerName].GetStats();
            foreach (var point in reforgeData.Dps.Keys)
            {
                var stats = 0;
                bool haste = false, crit = false, mastery = false, vers = false;
                int total = gear.crit + gear.haste + gear.mastery + gear.vers;
                double x = 0, y = 0, z = reforgeData.Dps[point];
                if(point.Crit != 0)
                {
                    stats++;;
                    point.Crit += gear.crit;
                    crit = true;
                }
                if (point.Haste != 0)
                {
                    stats++;
                    point.Haste += gear.haste;
                    haste = true;
                }
                if (point.Mastery != 0)
                {
                    stats++;
                    point.Mastery += gear.mastery;
                    mastery = true;
                }
                if (point.Vers != 0)
                {
                    stats++;
                    point.Vers += gear.vers;
                    vers = true;
                }
                if(stats == 2)
                {
                    if (haste)
                    {
                        x = (double)point.Haste / (double)total;
                        if(crit)
                        {
                            y = (double)point.Crit / (double)total;
                        } else if(mastery)
                        {
                            y = (double)point.Mastery / (double)total;
                        } else if(vers)
                        {
                            y = (double)point.Vers / (double)total;
                        }
                    }
                    else if (crit)
                    {
                        x = (double)point.Crit / (double)total;
                        if (mastery)
                        {
                            y = (double)point.Mastery / (double)total;
                        }
                        else if (vers)
                        {
                            y = (double)point.Vers / (double)total;
                        }
                    }
                    else if (mastery)
                    {
                        x = (double)point.Mastery / (double)total;
                        y = (double)point.Vers / (double)total;
                    }
                }
                if(stats == 3)
                {
                    if(haste)
                    {
                        x = (double)point.Haste / (double)total;
                        if(crit)
                        {
                            if(mastery)
                            {
                                y = (double)(point.Crit - point.Mastery) / (double)total;
                            }
                            else if (vers)
                            {
                                y = (double)(point.Crit - point.Vers) / (double)total;
                            }
                        } else if(mastery)
                        {
                            y = (double)(point.Mastery - point.Vers) / (double)(total);
                        }
                    }
                    else if(crit)
                    {
                        x = (double)point.Crit / (double)total;
                        y = (double)(point.Mastery - point.Vers) / (double)total;
                    }
                }
                if(stats == 4)
                {
                    x = (double)(point.Haste - point.Crit) / (double)total;
                    y = (double)(point.Mastery - point.Vers) / (double)total;
                }
                pointList.Add(new Point3D(x, y, z));
            }
            return CreateDataMatrixFromPoints(pointList);
        }

        public double[,] FindGradientY(Point3D[,] data)
        {
            int n = data.GetUpperBound(0) + 1;
            int m = data.GetUpperBound(0) + 1;
            var K = new double[n, m];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < m; j++)
                {
                    // Finite difference approximation
                    var p10 = data[i + 1 < n ? i + 1 : i, j - 1 > 0 ? j - 1 : j];
                    var p00 = data[i - 1 > 0 ? i - 1 : i, j - 1 > 0 ? j - 1 : j];
                    var p11 = data[i + 1 < n ? i + 1 : i, j + 1 < m ? j + 1 : j];
                    var p01 = data[i - 1 > 0 ? i - 1 : i, j + 1 < m ? j + 1 : j];

                    //double dx = p01.X - p00.X;
                    //double dz = p01.Z - p00.Z;
                    //double Fx = dz / dx;

                    double dy = p10.Y - p00.Y;
                    double dz = p10.Z - p00.Z;

                    K[i, j] = dz / dy;
                }
            return K;
        }

        public void SaveCSVs(string guid)
        {

        }

        public void ProcessCSVs(Model model, string guid)
        {
            _reforgeResults.ForEach((reforge) =>
            {
                var reforgeLines = reforge.Item1.Split('\n');
                var splitIndex = reforge.Item2.IndexOf('_');
                var time = reforge.Item2.Substring(0, splitIndex);
                var fight = reforge.Item2.Substring(splitIndex + 1).Split('.')[0];
                var timelessFight = reforge.Item2.Split('.')[0];
                if (model.model.ContainsKey(timelessFight) || model.model.ContainsKey(fight))
                {
                    if (model.timeModel.Count == 0 || model.timeModel.ContainsKey(time))
                    {
                        double modelWeight = model.model.ContainsKey(timelessFight) ? model.model[timelessFight] : model.model[fight];
                        double timeWeight = model.timeModel.Count != 0 ? model.timeModel[time] : 1.0;
                        var characterDefinitionRegex = new Regex("([^ ]+) Reforge Plot Results:");
                        var variableDefinitionRegex = new Regex("(?:[^ ]+, )*(?:[^ ]+)");
                        var valueRegex = new Regex("(?:[^, ]+, +)+\\r");
                        var currentName = "";
                        var mapping = new List<string>();
                        PlayerReforge currentReforge = null;
                        reforgeLines.ToList().ForEach((reforgeLine) =>
                        {
                            var charLine = characterDefinitionRegex.Match(reforgeLine);
                            if (charLine.Success)
                            {
                                currentName = charLine.Groups[1].Value;
                                if (!reforges.ContainsKey(currentName))
                                {
                                    reforges[currentName] = new PlayerReforge(currentName);
                                }
                                currentReforge = reforges[currentName];
                            }
                            else if (valueRegex.IsMatch(reforgeLine))
                            {
                                var values = reforgeLine.Split(',').ToList();
                                values = values.Take(values.Count() - 1).Select(x => x.Trim()).ToList();
                                var statPoints = values.Take(values.Count - 2);
                                var point = new ReforgePoint();
                                for(int i = 0; i < statPoints.Count() && i < mapping.Count; i++)
                                {
                                    if (mapping[i].Equals("crit"))
                                    {
                                        point.Crit = int.Parse(statPoints.ElementAt(i));
                                    }
                                    if (mapping[i].Equals("haste"))
                                    {
                                        point.Haste = int.Parse(statPoints.ElementAt(i));
                                    }
                                    if (mapping[i].Equals("mastery"))
                                    {
                                        point.Mastery = int.Parse(statPoints.ElementAt(i));
                                    }
                                    if (mapping[i].Equals("versatility"))
                                    {
                                        point.Vers = int.Parse(statPoints.ElementAt(i));
                                    }
                                }
                                var dpsError = double.Parse(values.Last());
                                var dps = double.Parse(values[values.Count - 2]);
                                if (currentReforge != null)
                                {
                                    if(!currentReforge.Dps.ContainsKey(point))
                                    {
                                        currentReforge.Dps[point] = 0.0;
                                    }
                                    if (!currentReforge.DpsError.ContainsKey(point))
                                    {
                                        currentReforge.DpsError[point] = 0.0;
                                    }
                                    currentReforge.Dps[point] += dps * modelWeight * timeWeight;
                                    currentReforge.DpsError[point] += dpsError * modelWeight * timeWeight;
                                }
                            }
                            else if (variableDefinitionRegex.IsMatch(reforgeLine))
                            {
                                var values = reforgeLine.Split(',').Select(x => x.Trim()).ToList();
                                values.ForEach((value) =>
                                {
                                    if(value.Contains("rating"))
                                    {
                                        var stat = value.Split('_')[0];
                                        mapping.Add(stat);
                                    }
                                });
                            }
                        });
                    }
                }
            });
            SaveCSVs(guid);
        }

        public void MergeResults(Model model, string guid)
        {
            CultureInfo currentCulture = Thread.CurrentThread.CurrentCulture;
            Tag = "";
            if (currentCulture.DateTimeFormat.ShortDatePattern.IndexOf("M") < currentCulture.DateTimeFormat.ShortDatePattern.IndexOf("d"))
            {
                Tag = DateTime.Today.Month.ToString().PadLeft(2, '0') + DateTime.Today.Day.ToString().PadLeft(2, '0') + DateTime.Today.Year.ToString();
            }
            else
            {
                Tag = DateTime.Today.Day.ToString().PadLeft(2, '0') + DateTime.Today.Month.ToString().PadLeft(2, '0') + DateTime.Today.Year.ToString();
            }
            Tag += "-" + guid;
            playerCritValues.Clear();
            playerDpsValues.Clear();
            playerHasteValues.Clear();
            playerMainStatValues.Clear();
            playerMainStatTypes.Clear();
            playerMasteryValues.Clear();
            playerVersValues.Clear();
            double minDps = double.MaxValue;
            ModelName = model.name.UppercaseWords();
            ModelNameShort = model.dispName;
            _results.ToList().ForEach((result) =>
            {
                var splitIndex = result.Item2.IndexOf('_');
                var time = result.Item2.Substring(0, splitIndex);
                var fight = result.Item2.Substring(splitIndex + 1).Split('.')[0];
                var timelessFight = result.Item2.Split('.')[0];
                if(model.model.ContainsKey(timelessFight) || model.model.ContainsKey(fight))
                {
                    if (model.timeModel.Count == 0 || model.timeModel.ContainsKey(time))
                    {
                        double modelWeight = model.model.ContainsKey(timelessFight) ? model.model[timelessFight] : model.model[fight];
                        double timeWeight = model.timeModel.Count != 0 ? model.timeModel[time]: 1.0;
                        result.Item1.sim.players.ForEach((player) =>
                        {
                            double dps = player.collected_data.dps.mean;
                            double damage = player.collected_data.dmg.mean;
                            string mainstat = "";
                            double mainstatValue = 0.0;
                            players[player.name] = player;
                            if (player.scale_factors != null)
                            {
                                if (player.scale_factors.Int > 0)
                                {
                                    mainstat = "Intellect";
                                    mainstatValue = player.scale_factors.Int;
                                }
                                else if (player.scale_factors.Agi > 0)
                                {
                                    mainstat = "Agility";
                                    mainstatValue = player.scale_factors.Agi;
                                }
                                else if (player.scale_factors.Str > 0)
                                {
                                    mainstat = "Strength";
                                    mainstatValue = player.scale_factors.Str;
                                }
                            }
                            if (!playerCritValues.ContainsKey(player.name))
                            {
                                playerCritValues[player.name] = 0.0;
                            }
                            if (!playerDpsValues.ContainsKey(player.name))
                            {
                                playerDpsValues[player.name] = 0.0;
                            }
                            if (!playerDamageValues.ContainsKey(player.name))
                            {
                                playerDamageValues[player.name] = 0.0;
                            }
                            if (!playerHasteValues.ContainsKey(player.name))
                            {
                                playerHasteValues[player.name] = 0.0;
                            }
                            if (!playerMainStatValues.ContainsKey(player.name))
                            {
                                playerMainStatValues[player.name] = 0.0;
                            }
                            if (!playerMainStatTypes.ContainsKey(player.name) && mainstat.Length > 0)
                            {
                                playerMainStatTypes[player.name] = mainstat;
                            }
                            if (!playerMasteryValues.ContainsKey(player.name))
                            {
                                playerMasteryValues[player.name] = 0.0;
                            }
                            if (!playerVersValues.ContainsKey(player.name))
                            {
                                playerVersValues[player.name] = 0.0;
                            }
                            if (!playerClasses.ContainsKey(player.name))
                            {
                                var specClass = player.specialization.Replace("Death K", "Deathk").Replace("Demon H", "Demonh").Replace("Beast M", "Beastm").Split(' ');
                                playerClasses[player.name] = specClass[1];
                                playerSpecs[player.name] = specClass[0];
                            }
                            playerDpsValues[player.name] += modelWeight * timeWeight * dps;
                            playerDamageValues[player.name] += modelWeight * timeWeight * damage;
                            if (player.scale_factors != null)
                            {
                                playerCritValues[player.name] += modelWeight * timeWeight * player.scale_factors.Crit;
                                playerHasteValues[player.name] += modelWeight * timeWeight * player.scale_factors.Haste;
                                playerMainStatValues[player.name] += modelWeight * timeWeight * mainstatValue;
                                playerMasteryValues[player.name] += modelWeight * timeWeight * player.scale_factors.Mastery;
                                playerVersValues[player.name] += modelWeight * timeWeight * player.scale_factors.Vers;
                            }
                        });
                    }
                }
            });
            List<PlayerResult> sublist = new List<PlayerResult>();
            foreach(string key in playerDpsValues.Keys)
            {
                var playerRes = new PlayerResult();
                playerRes.Dps = playerDpsValues[key];
                if (playerRes.Dps < minDps)
                {
                    minDps = playerRes.Dps;
                }
                playerRes.Name = key;
                playerRes.Damage = playerDamageValues[key];
                playerRes.Class = playerClasses[key];
                playerRes.ClassReadable = playerRes.Class.Replace("Deathk", "Death K").Replace("Demonh", "Demon H");
                playerRes.Spec = playerSpecs[key];
                playerRes.SpecReadable = playerRes.Spec.Replace("Beastm", "Beast M");

                if (playerMainStatTypes.ContainsKey(key))
                {
                    playerRes.MainstatType = playerMainStatTypes[key];
                    playerRes.MainstatValue = playerMainStatValues[key] / playerMainStatValues[key];
                    if (playerHasteValues.ContainsKey(key))
                    {
                        playerRes.Haste = playerHasteValues[key] / playerMainStatValues[key];
                    }
                    if (playerCritValues.ContainsKey(key))
                    {
                        playerRes.Crit = playerCritValues[key] / playerMainStatValues[key];
                    }
                    if (playerMasteryValues.ContainsKey(key))
                    {
                        playerRes.Mastery = playerMasteryValues[key] / playerMainStatValues[key];
                    }
                    if (playerVersValues.ContainsKey(key))
                    {
                        playerRes.Vers = playerVersValues[key] / playerMainStatValues[key];
                    }
                } else
                {
                    playerRes.MainstatType = "";
                }
                sublist.Add(playerRes);
            }

            sublist.ForEach((list) =>
            {
                if(list.Dps != minDps)
                {
                    list.DpsBoost = "(" + (((list.Dps / minDps) - 1) * 100.0).ToString("F2") + "%)";
                } else
                {
                    list.DpsBoost = "";
                }
            });
            MergedResults.Clear();
            sublist.OrderByDescending(player => player.Dps).ToList().ForEach((list) =>
            {
                MergedResults.Add(list);
            });
            SaveResults(guid);
            SelectedPlayer = MergedResults[0];

            ProcessCSVs(model,guid);
        }

        public void SaveResults(string guid)
        {
            var resultJson = JsonConvert.SerializeObject(MergedResults.ToList());
            if(!Directory.Exists("savedResults"))
            {
                Directory.CreateDirectory("savedResults");
            }
            
            string dir = "savedResults" + Path.DirectorySeparatorChar + Tag;
            int suffix = 0;
            string fixedDir = dir;
            if(Directory.Exists(dir))
            {
                fixedDir = dir + suffix;
            }
            while(Directory.Exists(fixedDir))
            {
                fixedDir = dir + ++suffix;
            }
            dir = fixedDir;
            Directory.CreateDirectory(dir);

            File.WriteAllText(dir + Path.DirectorySeparatorChar + "ModelName.txt", ModelName);
            File.WriteAllText(dir + Path.DirectorySeparatorChar + "ModelNameShort.txt", ModelNameShort);
            File.WriteAllText(dir + Path.DirectorySeparatorChar + "MergedResults.json", resultJson);
        }

        public string GetPawnString(PlayerResult player)
        {
            if(player.MainstatType == "")
            {
                return "No Pawn strings were generated in this run.";
            }
            string pawnString = "( Pawn: v1: \"" + player.Name + "_" + ModelNameShort + "_selfsim\": Class=" + player.Class + ", Spec=" + player.Spec + ", " + player.MainstatType + "=" + player.MainstatValue.ToString("F4");
            if(player.Haste > 0.0)
            {
                pawnString += ", HasteRating=" + player.Haste.ToString("F4");
            }
            if (player.Crit > 0.0)
            {
                pawnString += ", CritRating=" + player.Crit.ToString("F4");
            }
            if (player.Mastery > 0.0)
            {
                pawnString += ", MasteryRating=" + player.Mastery.ToString("F4");
            }
            if (player.Vers > 0.0)
            {
                pawnString += ", Versatility=" + player.Vers.ToString("F4");
            }
            pawnString += " )";
            return pawnString;
        }

        public string GetSummaryString(PlayerResult player)
        {
            return string.Format("{0}  -  {1:n0} DPS ( {2:n0} Total )", player.Name, player.Dps, player.Damage);
        }

        public void LoadResults(string tag)
        {
            SimDataManager.ResetSimData();
            if (!Directory.Exists("savedResults"))
            {
                return;
            }
            string dir = "savedResults" + Path.DirectorySeparatorChar + tag;
            if (Directory.Exists(dir))
            {
                if(!File.Exists(dir + Path.DirectorySeparatorChar + "MergedResults.json"))
                {
                    MessageBox.Show("Could not find any results. They may have been deleted.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    RunningFailed(this, new EventArgs());
                }
                var resultJson = File.ReadAllText(dir + Path.DirectorySeparatorChar + "MergedResults.json");
                MergedResults.Clear();
                JsonConvert.DeserializeObject<List<PlayerResult>>(resultJson).ToList().ForEach(x => MergedResults.Add(x));
                Tag = tag;
                if(File.Exists(dir + Path.DirectorySeparatorChar + "ModelName.txt"))
                {
                    ModelName = File.ReadAllText(dir + Path.DirectorySeparatorChar + "ModelName.txt");
                }
                if (File.Exists(dir + Path.DirectorySeparatorChar + "ModelNameShort.txt"))
                {
                    ModelNameShort = File.ReadAllText(dir + Path.DirectorySeparatorChar + "ModelNameShort.txt");
                }
                // Deserialize ModelNameShort and ModelName
                if (MergedResults.Count > 0)
                {
                    SelectedPlayer = MergedResults[0];
                }
            }
        }
    }
}