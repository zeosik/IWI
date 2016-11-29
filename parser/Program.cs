using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parser
{
    class Program
    {
        public class Category
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<Article> Articles { get; set; }
            public List<Tuple<Category, double>> SimilarCategories { get; set; }
            public Dictionary<string, Tuple<Category, double, int>> SimilarCategories2 { get; set; }
        }

        public class Article
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public List<Feature> Features { get; set; }
            public List<Category> Categories { get; set; }
        }

        public class Feature
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public double Value { get; set; }
        }

        static void Main(string[] args)
        {
            var dir = @"";
            var categoriesFile = Path.Combine(dir, "cats");
            var catsString = File.ReadAllLines(categoriesFile);
            var cats = new Dictionary<string, Category>();
            foreach (var line in catsString)
            {
                var words = line.Split(' ', '\t');
                cats.Add(words[1], new Category { Id = words[1], Name = words[0].Replace('_', ' '), Articles = new List<Article>(), SimilarCategories = new List<Tuple<Category, double>>(), SimilarCategories2 = new Dictionary<string, Tuple<Category, double, int>>() });
            }

            foreach (var cat in cats.Values)
            {
                //Console.WriteLine("{0}: {1}", cat.Id, cat.Name);
            }

            var featuresFile = Path.Combine(dir, "features");
            var features = new Dictionary<string, Feature>();
            var featuresString = File.ReadAllLines(featuresFile);
            foreach (var line in featuresString)
            {
                var words = line.Split(' ', '\t');
                features.Add(words[1], new Feature { Id = words[1], Name = words[0].Replace('_', ' ') });
            }

            var articlesFile = Path.Combine(dir, "articles");
            var articles = new Dictionary<string, Article>();
            var articlesByNameDict = new Dictionary<string, Article>();
            var articlesString = File.ReadAllLines(articlesFile);
            foreach (var line in articlesString)
            {
                var words = line.Split(' ', '\t');
                var a = new Article { Id = words[1], Name = words[0].Replace('_', ' '), Categories = new List<Category>() };
                articles.Add(words[1], a);
                articlesByNameDict.Add(a.Name, a);
            }

            var articlesFeatures = Path.Combine(dir, "list");
            var articlesFeaturesString = File.ReadAllLines(articlesFeatures);
            foreach (var line in articlesFeaturesString)
            {
                var words = line.Split('#');
                var id = words[0];
                var article = articles[id];
                var fts = words[1];
                if (string.IsNullOrWhiteSpace(fts))
                {
                    Console.WriteLine("no features for article {0} ", id);
                    continue;
                }
                var featuresValues = fts.Split(' ', '\t');
                article.Features = new List<Feature>();
                foreach (var fv in featuresValues)
                {
                    var words2 = fv.Split(new char[] { '-' }, 2);
                    var fid = words2[0];
                    var value = double.Parse(words2[1], CultureInfo.InvariantCulture);
                    if (!features.ContainsKey(fid))
                    {
                        Console.WriteLine("Skipping feature {0}", fid);
                        continue;
                    }
                    var f = features[fid];
                    article.Features.Add(new Feature() { Name = f.Name, Value = value });
                }
            }

            var articlesCats = Path.Combine(dir, "cats_tree");
            var articlesCatsString = File.ReadAllLines(articlesCats);
            foreach (var line in articlesCatsString)
            {
                var words = line.Split(' ', '\t');
                var id = words[0];
                var art = articles[id];
                for (int i = 1; i < words.Length; i++)
                {
                    var cat = cats[words[i]];
                    cat.Articles.Add(art);
                }
            }
            foreach(var cat in cats.Values)
            {
                foreach(var cat_art in cat.Articles)
                {
                    articles[cat_art.Id].Categories.Add(cat);
                }
            }
            var categoriesReal = Path.Combine(dir, "categories_real");
            var categoriesRealString = File.ReadAllLines(categoriesReal);
            var catParentChild = new Dictionary<Category, List<Category>>();
            foreach (var line in categoriesRealString)
            {
                var words = line.Split(' ', '\t');
                var cat = words[0];
                foreach (var childcat in words.Skip(1))
                {
                    if (cats.ContainsKey(cat) && cats.ContainsKey(childcat))
                    {
                        if (!catParentChild.ContainsKey(cats[cat]))
                        {
                            catParentChild.Add(cats[cat], new List<Category>());
                        }
                        catParentChild[cats[cat]].Add(cats[childcat]);
                    }
                }
            }
            
            //mapa kategori wikipedi, wszystkie kategorie nadrzedne i droga do ich podkategori
            var realCatMap = new Dictionary<Category, Dictionary<Category, int>>();
            int dist = 1;
            foreach (var p in catParentChild)
            {
                realCatMap.Add(p.Key, p.Value.ToDictionary(v => v, v => 1));
            }
            var toAdd = new Dictionary<Category, List<Category>>();
            ++dist;
            do
            {
                toAdd.Clear();
                foreach(var parent in realCatMap)
                {
                    foreach(var child in parent.Value)
                    {
                        if (realCatMap.ContainsKey(child.Key))
                        {
                            foreach(var newChild in realCatMap[child.Key])
                            {
                                if (!parent.Value.ContainsKey(newChild.Key))
                                {
                                    if (!toAdd.ContainsKey(parent.Key))
                                    {
                                        toAdd.Add(parent.Key, new List<Category>());
                                    }
                                    toAdd[parent.Key].Add(newChild.Key);
                                }
                            }
                        }
                    }
                }
                foreach(var a in toAdd)
                {
                    foreach(var c in a.Value) {
                        if (!realCatMap[a.Key].ContainsKey(c))
                        {
                            realCatMap[a.Key].Add(c, dist);
                        }
                    }
                }
                ++dist;
            } while (toAdd.Any());

            if (false)
            {
                foreach(var cat in cats.Values)
                {
                    var name = cat.Name;
                    var parts = name.Split(' ');
                    foreach (var otherCat in cats.Values)
                    {          
                        if(otherCat.Id == cat.Id)
                        {
                            continue;
                        }

                        double min = double.MaxValue;
                        foreach(var part in parts)
                        {
                            double sum = 0;
                            foreach (var art in otherCat.Articles)
                            {      
                                //TODO zoptymalizowac single                      
                                var value = art.Features.SingleOrDefault(x => x.Name.ToLower() == part.ToLower());
                                if (value != null)
                                {
                                    sum += value.Value;
                                }
                            }
                            if (sum < min)
                            {
                                min = sum;
                            }
                        }
                        cat.SimilarCategories.Add(new Tuple<Category, double>(otherCat, min));
                    }
                }

                foreach (var cat in cats.Values)
                {
                    var best3 = cat.SimilarCategories.OrderByDescending(x => x.Item2).Take(7);
                    Console.WriteLine(cat.Name);
                    foreach (var c in best3)
                    {
                        if (c.Item2 > 0)
                            Console.WriteLine("     {0}: {1}", c.Item1.Name, c.Item2);
                        else
                            Console.WriteLine("     No match");
                    }
                }
            }
            else
            {
                //liczenie podobienstwa kategori na podstawie linkow
                foreach (var art in articles.Values)
                {
                    var art1_cats = art.Categories;
                    foreach (var f in art.Features)
                    {
                        var art2 = articlesByNameDict[f.Name];
                        if (art.Id != art2.Id)
                        {
                            var art2_cats = art2.Categories;
                            foreach (var cat1 in art1_cats)
                            {
                                foreach (var cat2 in art2_cats)
                                {
                                    if (cat1.Id != cat2.Id)
                                    {
                                        if (cat1.SimilarCategories2.ContainsKey(cat2.Id))
                                        {
                                            var old = cat1.SimilarCategories2[cat2.Id];
                                            cat1.SimilarCategories2.Remove(cat2.Id);
                                            cat1.SimilarCategories2.Add(cat2.Id, new Tuple<Category, double, int>(cat2, old.Item2 + f.Value, old.Item3 + 1));
                                        }
                                        else
                                        {
                                            cat1.SimilarCategories2.Add(cat2.Id, new Tuple<Category, double, int>(cat2, f.Value, 1));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                List<CatLinkResult> bestCats = new List<CatLinkResult>();
                //filtrowanie powiazan
                foreach( var cat in cats.Values)
                {
                    var thisGroupSimilar = groupSimilar(cat, cat.SimilarCategories2.Values.Where(x => x.Item2 >= 0.09), int.MaxValue, realCatMap);
                    if (thisGroupSimilar.Any())
                    {
                        var thisGroupBest = new GroupBest(thisGroupSimilar, cat);
                        Console.WriteLine("{0}", thisGroupBest.ToStringSummary());
                        {
                            foreach (var groupBest in thisGroupBest.best.OrderByDescending(x => x.val).Take(7))
                            {
                                Console.WriteLine("\t{0}", thisGroupBest.ToStringLink(groupBest));
                            }
                        }
                    }
                    //stare liczenie
                    //if (false)
                    //{
                    //    var bestSimCatsWithoutWikiLink = cat.SimilarCategories2.Values
                    //        .Where(x => x.Item2 / x.Item3 > 0.20)
                    //        .OrderByDescending(x => x.Item2 / x.Item3)
                    //        .Select(c => new Tuple<Tuple<Category, double, int>, CatDistResult>(c, CatDistResult.search(cat, c.Item1, realCatMap, 25)))
                    //        .Where(c =>
                    //        {
                    //            var search = c.Item2;
                    //            return search.left == -1 || (search.left >= 3 && search.right >= 3) || (search.left + search.right >= 7);
                    //        });
                    //    foreach (var simCat in bestSimCatsWithoutWikiLink)
                    //    {
                    //        bestCats.Add(new CatLinkResult() { category = cat, similarCategory = simCat.Item1.Item1, val = simCat.Item1.Item2 / simCat.Item1.Item3, num = simCat.Item1.Item3, dist = simCat.Item2 });
                    //    }
                    //}
                }
                //var newLinks = bestCats
                //    .Where(c => c.num > 5)
                //    .Select(s => {
                //        s.dist = CatDistResult.search(s.category, s.similarCategory, realCatMap, int.MaxValue);
                //        return s;
                //        })
                //    .Where(s => s.dist.right >= 2 && s.dist.left >= 2)
                //    .OrderByDescending(s => s.dist.left + s.dist.right)
                //    .ThenByDescending(c => c.num)
                //    .ThenByDescending(c => c.val)
                //    ;
                //foreach(var link in newLinks)
                //{
                //    Console.WriteLine("  {0} -> {1} v: {2} n: {3} p: {4} l: {5} r: {6} ", link.category.Name, link.similarCategory.Name, link.val, link.num, link.dist.parent.Name, link.dist.left, link.dist.right);
                //}
            }
            Console.WriteLine("Done");
            Console.Read();
        }

        public static IEnumerable<CatLinkResult> groupSimilar(Category parent, IEnumerable<Tuple<Category, double, int>> similarCategories, int searchDistThreshhold, Dictionary<Category, Dictionary<Category, int>> realCatMap)
        {
            var thisGroupSimilar = similarCategories.Select(x => new CatLinkResult() { category = parent, similarCategory = x.Item1, val = x.Item2 / x.Item3, num = x.Item3, dist = CatDistResult.search(parent, x.Item1, realCatMap, searchDistThreshhold) });
            return thisGroupSimilar;
        }

        public class GroupBest
        {
            public Category parent;
            public double avgDist;
            public double avgWikiDist;
            public IEnumerable<CatLinkResult> best;
            public IEnumerable<CatLinkResult> groupSimilar;
            public GroupBest(IEnumerable<CatLinkResult> groupSimilar, Category parent)
            {
                this.parent = parent;
                this.groupSimilar = groupSimilar;
                avgDist = groupSimilar.Average(x => x.val);
                avgWikiDist = groupSimilar.Average(x => x.dist.dist());
                best = groupSimilar.Where(x => x.val >= avgDist).Where(x => x.dist.dist() >= avgWikiDist);
            }

            public string ToStringLink(CatLinkResult link)
            {
                return string.Format("{0} \t dist:{1:0.00} n:{5} wikiDist:{2} l:{3} r:{4}", link.similarCategory.Name.PadRight(30, ' '), link.val, link.dist.dist(), link.dist.left, link.dist.right, link.num);
            }

            public string ToStringSummary()
            {
                return string.Format("{0}: avg:{1:0.00} wikiAvg:{2:0.00} all:{3} aboveAvg:{4}", parent.Name, avgDist, avgWikiDist, groupSimilar.Count(), best.Count());
            }
        }

        public class CatLinkResult
        {
            public Category category;
            public Category similarCategory;
            public double val;
            public int num;
            public CatDistResult dist;
        }

        public class CatDistResult
        {
            public Category parent;
            public int left;
            public int right;

            public int dist()
            {
                return left + right;
            }

            CatDistResult(Category p, int l, int r)
            {
                parent = p;
                left = l;
                right = r;
            }

            public static CatDistResult search(Category one, Category two, Dictionary<Category, Dictionary<Category, int>> distMap, int threshhold)
            {
                var result = oneIsParentOfTwo(one, two, distMap);
                if (result != null)
                {
                    return result;
                }
                result = oneIsParentOfTwo(two, one, distMap);
                if (result != null)
                {
                    return result;
                }
                var commonParents = allCommonParents(one, two, distMap, threshhold);
                var maxDist = commonParents.Max(r => r.dist());
                result = commonParents.First(r => r.dist() == maxDist);
                if (result == null)
                {
                    result = new CatDistResult(new Category() { Name = "Not found" }, -1, -1);
                }
                return result;
            }

            private static CatDistResult oneIsParentOfTwo(Category one, Category two, Dictionary<Category, Dictionary<Category, int>> distMap)
            {
                if (distMap.ContainsKey(one))
                {
                    if (distMap[one].ContainsKey(two))
                    {
                        var result = new CatDistResult(one, 0, distMap[one][two]);
                        return result;
                    }
                }
                return null;
            }
            private static List<CatDistResult> allCommonParents(Category one, Category two, Dictionary<Category, Dictionary<Category, int>> distMap, int threshhold)
            {
                var results = new List<CatDistResult>();
                int size = 0;
                foreach (var parent in distMap.Reverse())
                {
                    if (parent.Value.ContainsKey(one) && parent.Value.ContainsKey(two))
                    {
                        var r = new CatDistResult(parent.Key, parent.Value[one], parent.Value[two]);
                        results.Add(r);
                        if (++size >= threshhold)
                        {
                            break;
                        }
                    }
                }
                return results;
            }
        }
    }
}
