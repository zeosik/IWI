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
            public Dictionary<string, Tuple<Category, double, int>> SimilarCategories2 { get; set; }
        }

        public class CetegoryWordSimilarity
        {
            public Category category;
            public List<Tuple<Category, double, int>> SimilarCategories { get; set; }
        }

        public class Article
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public Dictionary<string, Feature> Features { get; set; } //byName
            public List<Category> Categories { get; set; }
        }

        public class Feature
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public double Value { get; set; }
        }
        
        static Dictionary<string, Category> cats;
        static Dictionary<string, Article> articles;
        static Dictionary<string, Article> articlesByNameDict;
        static Dictionary<Category, Dictionary<Category, int>> realCatMap;

        static void Main(string[] args)
        {
            Setup();

            if (true)
            {
                ByWords();
            }
            else
            {
                ByLinks();
            }
            Console.WriteLine("Done");
            //Console.Read();
        }

        static void Setup()
        {
            var dir = @"";
            var categoriesFile = Path.Combine(dir, "cats");
            var catsString = File.ReadAllLines(categoriesFile);
            cats = new Dictionary<string, Category>();
            foreach (var line in catsString)
            {
                var words = line.Split(' ', '\t');
                cats.Add(words[1], new Category { Id = words[1], Name = words[0].Replace('_', ' '), Articles = new List<Article>(), SimilarCategories2 = new Dictionary<string, Tuple<Category, double, int>>() });
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
            articles = new Dictionary<string, Article>();
            articlesByNameDict = new Dictionary<string, Article>();
            var articlesString = File.ReadAllLines(articlesFile);
            foreach (var line in articlesString)
            {
                var words = line.Split(' ', '\t');
                var a = new Article { Id = words[1], Name = words[0].Replace('_', ' '), Categories = new List<Category>(), Features = new Dictionary<string, Feature>()};
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
                article.Features = new Dictionary<string, Feature>();
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
                    article.Features.Add(f.Name, new Feature() { Name = f.Name, Value = value });
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
            foreach (var cat in cats.Values)
            {
                foreach (var cat_art in cat.Articles)
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
            realCatMap = new Dictionary<Category, Dictionary<Category, int>>();
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
                foreach (var parent in realCatMap)
                {
                    foreach (var child in parent.Value)
                    {
                        if (realCatMap.ContainsKey(child.Key))
                        {
                            foreach (var newChild in realCatMap[child.Key])
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
                foreach (var a in toAdd)
                {
                    foreach (var c in a.Value)
                    {
                        if (!realCatMap[a.Key].ContainsKey(c))
                        {
                            realCatMap[a.Key].Add(c, dist);
                        }
                    }
                }
                ++dist;
            } while (toAdd.Any());
        }

        static void ByWords()
        {
            var total = cats.Values.Count;
            var step = 0;
            var stemmer = new TextStemmerEN();
            var categories = cats.Values.Select(c => 
                new CetegoryWordSimilarity() { category = c, SimilarCategories = new List<Tuple<Category, double, int>>() });

            foreach (var cat in categories)
            {
                ++step;
                var progress = (double)step / total * 100.0;
                if (step % 100 == 0)
                {
                    Console.WriteLine("Progress: {0:0.00}%", progress);
                }
                var name = cat.category.Name;
                var parts = name.Split(' ');
                foreach (var otherCat in cats.Values)
                {
                    if (otherCat.Id == cat.category.Id)
                    {
                        continue;
                    }

                    double min = double.MaxValue;
                    int finalArticlesCount = 0;
                    foreach (var part in parts)
                    {
                        stemmer.add(part.ToLower().ToCharArray(), part.Length);
                        stemmer.stem();
                        var stemmedPart = stemmer.ToString();
                        double sum = 0;
                        int articlesCount = 0;
                        foreach (var art in otherCat.Articles)
                        {
                            if (art.Features.ContainsKey(stemmedPart))
                            {
                                articlesCount++;
                                var value = art.Features[stemmedPart];
                                sum += value.Value;
                            }
                        }
                        if (sum < min)
                        {
                            finalArticlesCount = articlesCount;
                            min = sum;
                        }
                    }
                    cat.SimilarCategories.Add(new Tuple<Category, double, int>(otherCat, min, finalArticlesCount));
                }

                var thisGroupSimilar = groupSimilarWords(cat.category, cat.SimilarCategories.Where(x => x.Item2 >= 5), int.MaxValue, realCatMap);
                if (thisGroupSimilar.Any())
                {
                    var thisGroupBest = new GroupBest(thisGroupSimilar, cat.category);
                    Console.WriteLine("{0}", thisGroupBest.ToStringSummary());
                    {
                        foreach (var groupBest in thisGroupBest.best.OrderByDescending(x => x.val/x.articlesCount).Take(7))
                        {
                            Console.WriteLine("\t{0}", thisGroupBest.ToStringLink(groupBest));
                        }
                    }
                }
                //zwalnaimy miejsce bo nie uzywamy juz tego
                cat.SimilarCategories.Clear();
            }
        }

        static void ByLinks()
        {
            //liczenie podobienstwa kategori na podstawie linkow
            foreach (var art in articles.Values)
            {
                var art1_cats = art.Categories;
                foreach (var f in art.Features.Values)
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
            foreach (var cat in cats.Values)
            {
                var thisGroupSimilar = groupSimilarLinks(cat, cat.SimilarCategories2.Values.Where(x => x.Item2 / x.Item3 >= 0.09), int.MaxValue, realCatMap);
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
            }
        }

        public static IEnumerable<CatLinkResult> groupSimilarLinks(Category parent, IEnumerable<Tuple<Category, double, int>> similarCategories, int searchDistThreshhold, Dictionary<Category, Dictionary<Category, int>> realCatMap)
        {
            var thisGroupSimilar = similarCategories.Select(x => new CatLinkResult() { category = parent, similarCategory = x.Item1, val = x.Item2 / x.Item3, num = x.Item3, dist = CatDistResult.search(parent, x.Item1, realCatMap, searchDistThreshhold) });
            return thisGroupSimilar;
        }

        public static IEnumerable<CatLinkResult> groupSimilarWords(Category parent, IEnumerable<Tuple<Category, double, int>> similarCategories, int searchDistThreshhold, Dictionary<Category, Dictionary<Category, int>> realCatMap)
        {
            var thisGroupSimilar = similarCategories.Select(x => new CatLinkResult() { category = parent, similarCategory = x.Item1, val = x.Item2, num = 1, dist = CatDistResult.search(parent, x.Item1, realCatMap, searchDistThreshhold), articlesCount = x.Item3});
            return thisGroupSimilar;
        }

        public class GroupBest
        {
            public Category parent;
            public double avgDist;
            public double avgWikiDist;
            public int articlesCount;
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
                return string.Format("{0} \t value:{1:0.00} articles count: {7} n:{5} wikiDist:{2} l:{3} r:{4} parent: {6}", link.similarCategory.Name.PadRight(30, ' '), link.val, link.dist.dist(), link.dist.left, link.dist.right, link.num, link.dist.parent.Name, link.articlesCount);
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
            public int articlesCount;
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
                if (commonParents.Any())
                {
                    var maxDist = commonParents.Max(r => r.dist());
                    result = commonParents.First(r => r.dist() == maxDist);
                }
                else
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
