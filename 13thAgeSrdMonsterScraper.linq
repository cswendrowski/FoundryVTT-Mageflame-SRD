<Query Kind="Program">
  <Reference>&lt;RuntimeDirectory&gt;\System.Web.dll</Reference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>ScrapySharp</NuGetReference>
  <Namespace>HtmlAgilityPack</Namespace>
  <Namespace>Microsoft.FSharp.Collections</Namespace>
  <Namespace>Microsoft.FSharp.Control</Namespace>
  <Namespace>Microsoft.FSharp.Core</Namespace>
  <Namespace>Microsoft.FSharp.Core.CompilerServices</Namespace>
  <Namespace>Microsoft.FSharp.Data.UnitSystems.SI.UnitNames</Namespace>
  <Namespace>Microsoft.FSharp.Linq</Namespace>
  <Namespace>Microsoft.FSharp.Linq.QueryRunExtensions</Namespace>
  <Namespace>Microsoft.FSharp.Linq.RuntimeHelpers</Namespace>
  <Namespace>Microsoft.FSharp.NativeInterop</Namespace>
  <Namespace>Microsoft.FSharp.Quotations</Namespace>
  <Namespace>Microsoft.FSharp.Reflection</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Bson</Namespace>
  <Namespace>Newtonsoft.Json.Converters</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Newtonsoft.Json.Schema</Namespace>
  <Namespace>Newtonsoft.Json.Serialization</Namespace>
  <Namespace>ScrapySharp.Cache</Namespace>
  <Namespace>ScrapySharp.Core</Namespace>
  <Namespace>ScrapySharp.Exceptions</Namespace>
  <Namespace>ScrapySharp.Extensions</Namespace>
  <Namespace>ScrapySharp.Html</Namespace>
  <Namespace>ScrapySharp.Html.Dom</Namespace>
  <Namespace>ScrapySharp.Html.Forms</Namespace>
  <Namespace>ScrapySharp.Html.Parsing</Namespace>
  <Namespace>ScrapySharp.Network</Namespace>
  <Namespace>System</Namespace>
  <Namespace>System.Collections.Generic</Namespace>
  <Namespace>System.Linq</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Runtime.Caching</Namespace>
  <Namespace>System.Runtime.Caching.Configuration</Namespace>
  <Namespace>System.Runtime.Caching.Hosting</Namespace>
  <Namespace>System.Text.RegularExpressions</Namespace>
  <Namespace>System.Web</Namespace>
</Query>

void Main()
{
	var monsters = new SrdMonsterScraper().Scrape();
	MonsterConverter.ConvertMonstersToJson(monsters);
}

public class SrdMonsterScraper
{
	private const string SrdMonsterUrl = "https://www.13thagesrd.com/monsters";

	public List<Monster> Scrape()
	{
		return ScrapePage(SrdMonsterUrl);
	}

	private List<Monster> ScrapePage(string url)
	{
		var web = new HtmlWeb();
		url = HttpUtility.UrlDecode(url);
		url = url.TrimEnd('?');
		if (!url.StartsWith("https://")) url = "https://" + url;
		var document = web.Load(url);
		var response = document.DocumentNode;
		
		var incorrectSrdMonsterNames = new List<string> { "Hydra", "Goblin", "Human", "Kobold", "Lizardman", "Medusa", "Orc", "Troglodyte", "Mummy", "Zombie", "Gnoll", "Half-Orc", "Skeleton" };

		var monsterNames = response.CssSelect("h4 > span").Select(x => x.InnerText).SkipWhile(x => x != "Wrecker").Skip(1)
			.Where(x => !incorrectSrdMonsterNames.Contains(x)).ToList();
			
		var derro = monsterNames.IndexOf("Derro");
		monsterNames.Insert(derro, "Derro Sage");
		monsterNames.Insert(derro, "Derro Maniac");
		monsterNames.Remove("Derro");
			
		var monsterDetails = response
			.CssSelect("table")
			.SkipWhile(x => !x.InnerText.Contains("Size/Strength regular, large, or huge; Level level; Role role; Type type"))
			.Skip(1)
			.Where(x => !x.InnerText.Contains("Save Penalty"))
			.TakeWhile( x => !x.InnerText.Contains("Speed"))
			.ToList();

		$"{monsterNames.Count} monster names, {monsterDetails.Count} details".Dump();

		var failures = 0;
		var toReturn = new List<Monster>();
		for (int x = 0; x < monsterNames.Count; x++)
		//for (int x = 1; x < 2; x++)
		{
			x.Dump();
			try
			{
				toReturn.Add(ScrapeMonster(monsterNames[x], monsterDetails[x]));
			}
			catch (Exception e)
			{
				failures++;
				$"{monsterNames[x]} failed!".Dump();
				e.Dump();
			}
		}

		$"There were {failures} scraping failures".Dump();
		return toReturn;
	}

	private Monster ScrapeMonster(string name, HtmlNode table)
	{
		var monster = new Monster()
		{
			Name = name
		};

		var tableInfo = table.ChildNodes[1];

		var standardInfo = tableInfo.ChildNodes[1];
		var attacksInfo = tableInfo.ChildNodes[3].ChildNodes[1];
		var defenseInfo = tableInfo.ChildNodes[7];

		GetStandardInfo(monster, standardInfo);
		GetDefenses(monster, defenseInfo);
		GetAttacks(monster, attacksInfo);

		//monster.Dump();
		return monster;
	}

	private static void GetAttacks(Monster monster, HtmlNode attacksInfo)
	{
		Attack currentAttack = null;
		bool areTraitsNastierSpecials = false;

		for (int x = 1; x <= attacksInfo.ChildNodes.Count - 1; x += 2)
		{
			var line = attacksInfo.ChildNodes[x];
			ProcessLine(monster, attacksInfo, ref currentAttack, ref areTraitsNastierSpecials, line);
		}
		monster.Attacks.Add(currentAttack);
	}

	private static void ProcessLine(Monster monster, HtmlNode attacksInfo, ref Attack currentAttack, ref bool areTraitsNastierSpecials, HtmlNode line)
	{
		"".Dump();
		
		var linesToIgnore = new List<string>() { "And it has one additional ability" };

		line.InnerHtml.Dump();
		
		if (linesToIgnore.Any(x => line.InnerText.StartsWith(x)))
		{
			$"Ignoring line".Dump();
		}
		else if ((!line.InnerHtml.StartsWith("<i>") || line.InnerText.Contains("[Special trigger]") || line.InnerText.Contains("[Group ability]")) && (line.InnerHtml.Contains("+") && line.InnerHtml.Contains("vs")) || line.InnerText.Contains("Ranged:"))
		{
			if (currentAttack != null)
			{
				monster.Attacks.Add(currentAttack);
			}
			areTraitsNastierSpecials = false;

			currentAttack = ProcessAttackHeader(line);
		}
		else if (line.InnerHtml.Contains("Nastier Specials"))
		{
			"Line indicates start of Nastier Specials".Dump();
			areTraitsNastierSpecials = true;
		}
		else if (Attack.AttackModifiers.Any(m => line.InnerHtml.Contains(m)))
		{
			"Line is an attack modifier".Dump();

			if (line.InnerHtml.Contains("Natural") || line.InnerHtml.Contains("Miss"))
			{
				currentAttack.Triggers.Add(new AttackTrigger()
				{
					Trigger = line.InnerText.Split(':').First().Trim(),
					Effect = ProcessStringWithHtmlAndRolls(line.InnerText.Split(':').Last().Trim())
				});
			}
			else
			{
				currentAttack.Description += line.InnerText;
			}
		}
		else if (line.InnerHtml.Contains("</i>"))
		{
			"Line is a Trait".Dump();
			
			var trait = new Trait()
			{
				Name = WebUtility.HtmlDecode(line.InnerText.Split(':').First().Trim()),
				Effect = ProcessStringWithHtmlAndRolls(line.InnerText.Split(':').Last().Trim())
			};

			if (areTraitsNastierSpecials)
			{
				monster.NastierSpecials.Add(trait);
			}
			else
			{
				monster.Traits.Add(trait);
			}
		}
		else if (line.InnerText.StartsWith("Vulnerability"))
		{
			"Line is a Vulnerability".Dump();
			monster.Vulnerability = line.InnerText.Replace("Vulnerability:", "").Trim();
		}
		else if (line.InnerText.StartsWith("Resistance:"))
		{
			"Line is a Resistance".Dump();
			monster.Resistance = line.InnerText.Replace("Resistance:", "").Trim();
		}
		else if (line.InnerText.Contains("attack bonus against any enemy it is grabbing."))
		{
			var trait = new Trait()
			{
				Name = "Strong grasp",
				Effect = line.InnerText
			};

			monster.Traits.Add(trait);
		}
		else if (line.InnerText.StartsWith("Engulf and dissolve:"))
		{
			var trait = new Trait()
			{
				Name = "Engulf and dissolve",
				Effect = WebUtility.HtmlDecode(line.InnerText.Replace("Engulf and dissolve:", ""))
			};

			monster.Traits.Add(trait);
		}
		else if (line.InnerText.Contains("After the first success, the grace die bumps up"))
		{
			MergeOrAddTrait("grace", "Graceful", monster, line);
		}
		else if (line.InnerText.StartsWith("Any engulfed creature"))
		{
			MergeOrAddTrait("Engulf", "Engulf", monster, line);
		}
		else if (line.InnerText.StartsWith("A wraith can move through solid objects"))
		{
			MergeOrAddTrait("Ghostly", "Ghostly", monster, line);
		}
		else if (line.InnerText == "Bite +6; 5 damage")
		{
			if (currentAttack != null)
			{
				monster.Attacks.Add(currentAttack);
			}
			areTraitsNastierSpecials = false;
			
			currentAttack = new Attack() {
				Name = "Bite",
				Bonus = 6,
				Damage = "5 damage",
				Target = "AC"
			};
		}
		else
		{
			throw new Exception("Unknown line! " + line.InnerText);
		}
	}

	private static void MergeOrAddTrait(string key, string name, Monster monster, HtmlNode line)
	{
		var trait = monster.Traits.FirstOrDefault(x => x.Name.Contains(key));

		if (trait != null)
		{
			trait.Effect += " " + WebUtility.HtmlDecode(line.InnerText);
		}
		else
		{
			monster.Traits.Add(new Trait()
			{
				Name = name,
				Effect = WebUtility.HtmlDecode(line.InnerText)
			});
		}
	}

	private static string ProcessStringWithHtmlAndRolls(string toProcess)
	{
		var toReturn = WebUtility.HtmlDecode(toProcess);
		
		var diceRegex = @"\d*d\d+";
		var diceRegexMatches = Regex.Matches(toReturn, diceRegex);
		
		foreach (Match match in diceRegexMatches)
		{
			if (match.Success)
			{
				var toReplace = match.Value;
				toReturn = toReturn.Replace(toReplace, $"[[{toReplace}]]");
			}
		}
		
		return toReturn;
	}

	private static Attack ProcessAttackHeader(HtmlNode line)
	{
		"Line is an attack header".Dump();

		var cleanLine = ProcessStringWithHtmlAndRolls(line.InnerText)
			.Replace("<b>", "").Replace("</b>", "")
			.Replace("<i>", "").Replace("</i>", "")
			.Replace(") – ", ");") 
			.Replace("—", ";");

		var targetDetailsRegex = @"\(.*?\)";
		var targetDetailsRegexMatch = Regex.Match(cleanLine, targetDetailsRegex);

		if (targetDetailsRegexMatch.Success)
		{
			var toReplace = targetDetailsRegexMatch.Groups[0].Value;
			var replacement = toReplace.Replace(";", "<SEMICOLON>");
			$"Replacing '{toReplace}' with '{replacement}'".Dump();
			cleanLine = cleanLine.Replace(toReplace, replacement);
		}
		cleanLine.Dump();

		if (cleanLine.Contains("Magic missile"))
		{
			var magicMissleRegex = @"(.*)(\(.*?\))[;,](.*)";
			var magicMissleRegexMatches = Regex.Match(cleanLine, magicMissleRegex);

			if (magicMissleRegexMatches.Success)
			{
				return new Attack()
				{
					Name = magicMissleRegexMatches.Groups[1].Value.Trim(),
					Bonus = -1,
					Target = magicMissleRegexMatches.Groups[2].Value.Trim().Replace("<SEMICOLON>", ";"),
					Damage = magicMissleRegexMatches.Groups[3].Value.Trim()
				};
			}
			else
			{
				throw new Exception($"Could not parse Magic Missle line '{cleanLine}'");
			}
		}

		var attackRegex = @"(.*)(\+ *(\d+)) vs\.* (.*?)[;,](.*)";
		var attackRegexMatches = Regex.Match(cleanLine, attackRegex);

		if (attackRegexMatches.Success)
		{
			return new Attack()
			{
				Name = attackRegexMatches.Groups[1].Value.Trim(),
				Bonus = int.Parse(attackRegexMatches.Groups[3].Value.Trim()),
				Target = attackRegexMatches.Groups[4].Value.Trim().Replace("<SEMICOLON>", ";"),
				Damage = attackRegexMatches.Groups[5].Value.Trim()
			};
		}
		else
		{
			throw new Exception($"Could not parse line '{cleanLine}'");
		}
	}

	private static void GetDefenses(Monster monster, HtmlNode defense)
	{
		monster.AC = int.Parse(defense.ChildNodes[1].InnerText.Trim());
		monster.PD = int.Parse(defense.ChildNodes[3].InnerText.Trim());
		monster.MD = int.Parse(defense.ChildNodes[5].InnerText.Trim());
		monster.HP = int.Parse(defense.ChildNodes[7].InnerText.Replace("(each)", "").Trim());
	}

	private static void GetStandardInfo(Monster monster, HtmlNode standardInfo)
	{
		var typeInfo = standardInfo.ChildNodes[1];
		var cleanText = typeInfo.InnerText
			.Replace("2x", "Size/Strength Double Strength")
			.Replace("X2", "Size/Strength Double Strength")
			.Replace("3x", "Size/Strength Triple Strength");
		
		var infoRegex = @"Size/Strength (.*); .*?(\d+).*; Role (.*); Type (.*)";
		var infoRegexMatches = Regex.Match(cleanText, infoRegex);

		if (infoRegexMatches.Success)
		{
			monster.Size = infoRegexMatches.Groups[1].Value.Trim();
			monster.Level = int.Parse(infoRegexMatches.Groups[2].Value.Trim());
			monster.Role = infoRegexMatches.Groups[3].Value.Trim();
			monster.Type = infoRegexMatches.Groups[4].Value.Trim();
		}
		else
		{
			throw new Exception($"Couldn't parse Standard Info - '{cleanText}'");
		}

		monster.Initiative = int.Parse(standardInfo.ChildNodes[3].ChildNodes[1].InnerText.Replace("+", "").Trim());
	}
}

public class Monster
{
	public string Name { get; set; }
	public string Size { get; set; }
	public int Level { get; set; }
	public string Role { get; set; }
	public string Type { get; set; }

	public int Initiative { get; set; }
	public int AC { get; set; }
	public int PD { get; set; }
	public int MD { get; set; }
	public int HP { get; set; }
	
	public string Vulnerability { get; set; } = "";
	public string Resistance { get; set; } = "";

	public List<Attack> Attacks { get; set; } = new List<Attack>();

	public List<Trait> Traits { get; set; } = new List<Trait>();
	public List<Trait> NastierSpecials { get; set; } = new List<Trait>();
}

public class Attack
{
	public static List<string> AttackModifiers = new List<string>() { "Natural", "Limited use", "Miss" };

	public string Name { get; set; }
	public int Bonus { get; set; }
	public string Target { get; set; } = "";
	public string Damage { get; set; }
	public List<AttackTrigger> Triggers = new List<AttackTrigger>();
	public string Description { get; set; } = "";

	public string Roll {
		get
		{
			if (Bonus == -1) {
				return "Automatic hit";
			}
			return $"[[d20 + {Bonus}]] vs {Target}";
		}
	}
}

public class AttackTrigger
{
	public string Trigger { get; set; }
	public string Effect { get; set; }
}

public class Trait
{
	public string Name { get; set; }
	public string Effect { get; set; }
}


public static class MonsterConverter
{
	public static void ConvertMonstersToJson(List<Monster> monsters) {
	
		var convertedMonsters = new List<ActorArchmageData>();

		var failures = 0;
		foreach (var monster in monsters)
		{
			try
			{
				var converted = ConvertMonsterToArchmageData(monster);
				convertedMonsters.Add(converted);
				//converted.Dump();
			}
			catch (Exception e)
			{
				$"{monster.Name} failed conversion".Dump();
				e.Dump();
				failures++;
			}
		}

		$"There were {failures} failures in converting".Dump();
		
		var jsonSerializationSettings = new JsonSerializerSettings() {
			NullValueHandling = NullValueHandling.Ignore,
			Formatting = Newtonsoft.Json.Formatting.Indented
		};
		JsonConvert.SerializeObject(convertedMonsters, jsonSerializationSettings).Dump();
	}
	
	private static ActorArchmageData ConvertMonsterToArchmageData(Monster monster) {
		var toReturn = new ActorArchmageData() {
			data = new ActorData() {
//				abilities = new Abilities() {
//					
//				},
				attributes = new Attributes() {
					ac = new Score("Armor Class", monster.AC) { baseScore = 10, min = 0 },
					hp = new Score("Hit Points", monster.HP) { baseScore = 10, min = 0, max = monster.HP },
					init = new Score("Initiative Modifier", monster.Initiative - monster.Level) { mod = monster.Level + monster.Initiative },
					level = new Score("Level", monster.Level) { min = 0, max = 12 },
					md = new Score("Mental Defense", monster.MD) { baseScore = 10, min = 0 },
					pd = new Score("Physical Defense", monster.PD) { baseScore = 10, min = 0 },
				},
				details = new Details()
				{
					level = new Score("Level", monster.Level) { min = 0, max = 12 },
					resistance = new UserQuery.Value<string>("Resistance", monster.Resistance) { type = "String" },
					vulnerability = new UserQuery.Value<string>("Vulnerability", monster.Vulnerability) { type = "String" }
				}
			},
			name = monster.Name
		};
		
		var amtAdded = 0;

		foreach (var attack in monster.Attacks)
		{
			var actionData = new ActionData()
			{
				attack = new UserQuery.Value<string>("Attack Roll", attack.Roll),
				description = new UserQuery.Value<string>("Description", attack.Description),
				hit = new UserQuery.Value<string>("Hit", attack.Damage)
			};

			foreach (var trigger in attack.Triggers) {
				if (trigger.Trigger.Contains("Miss")) {
					actionData.miss = new UserQuery.Value<string>("Hit", trigger.Effect) { name = trigger.Trigger };
				}
				else if (actionData.hit1 == null) {
					actionData.hit1 = new UserQuery.Value<string>("Hit", trigger.Effect) { name = trigger.Trigger };
				}
				else if (actionData.hit2 == null)
				{
					actionData.hit2 = new UserQuery.Value<string>("Hit", trigger.Effect) { name = trigger.Trigger };
				}
				else if (actionData.hitt3 == null)
				{
					actionData.hitt3 = new UserQuery.Value<string>("Hit", trigger.Effect) { name = trigger.Trigger };
				}
				else {
					throw new Exception("Ran out of space for Hit triggers!");
				}
			}

			toReturn.items.Add(new ActionItem() {
				name = attack.Name,
				sort = amtAdded++ * 100000,
				data = actionData
			});
		}
		
		foreach (var trait in monster.Traits) {
			toReturn.items.Add(new TraitItem() {
				name = trait.Name,
				sort = amtAdded++ * 100000,
				data = new TraitData() {
					description = new UserQuery.Value<string>("Description", trait.Effect)
				}
			});
		}

		foreach (var trait in monster.NastierSpecials)
		{
			toReturn.items.Add(new NastierItem()
			{
				name = trait.Name,
				sort = amtAdded++ * 100000,
				data = new TraitData()
				{
					description = new UserQuery.Value<string>("Description", trait.Effect)
				}
			});
		}

		return toReturn;
	}
}


public class ActorArchmageData {
	public ActorData data { get; set; }
	public string folder => "RojSIAWRQfItUAUV";
	public string img => "icons/svg/mystery-man.svg";
	public List<Item> items { get; set; } = new List<Item>();
	//public ActorToken token { get; set; }
	public string type => "npc";
	public string name { get; set; }
}

public class ActorData {
	public Abilities abilities { get; set; }
	
	public Attributes attributes { get; set; }
	
	//public Backgrounds backgrounds { get; set; }
	
	public Details details { get; set; }
}

public class Abilities {
	public Score cha { get; set; }
	
	public Score str { get; set; }
	
	public Score con { get; set; }
	
	public Score dex { get; set; }
	
	[JsonProperty("int")]
	public Score intellegence { get; set; }
	
	public Score wis { get; set; }
}

public class Attributes {
	public Score ac { get; set; }
	
	public Score hp { get; set; }
	
	public Score init { get; set; }
	
	public Score level { get; set; }
	
	public Score md { get; set; }
	
	public Score pd { get; set; }
	
	public Score recoveries { get; set; }
}

public class Details {
	public Score level { get; set; }
	
	public Value<string> resistance { get; set; }
	
	public Label role => new Label("Role");
	
	public Label size => new Label("Size");
	
	public Label type => new Label("Type");
	
	public Value<string> vulnerability { get; set; }
}

public abstract class Item {
	public string img => "icons/svg/mystery-man.svg";
	public string name { get; set; }
	public int sort { get; set; } = 0;

	public abstract string type { get; }
}

public class ActionItem : Item
{
	public override string type => "action";
	
	public ActionData data { get; set; }
}

public class TraitItem : Item
{
	public override string type => "trait";
	
	public TraitData data { get; set; }
}

public class NastierItem : TraitItem
{
	public override string type => "nastierSpecial";
}

public class ActionData {
	public Value<string> attack { get; set; }
	
	public Value<string> description { get; set; }
	
	public Value<string> hit { get; set; }
	
	public Value<string> hit1 { get; set; }
	public Value<string> hit2 { get; set; }
	public Value<string> hitt3 { get; set; }
	
	public Label hit3 => new Label("Hit");
	public Label hit4 => new Label("Hit");
	public Label hit5 => new Label("Hit");
	
	public Value<string> miss { get; set; }
	
	public Label name => new Label("Name");
}

public class TraitData {
	public Label name => new Label("Name");
	
	public Value<string> description { get; set; }
}

public class Label : Value<string> {
	public override string type { get; set; }= "String";
	
	public Label(string label) : base(label){
	}
}

public class Value<T>
{
	public virtual string type { get; set; } = "";

	public virtual string label { get; set; } = "";
	
	public virtual string name { get; set; } = null;
	
	public T value { get; set; }

	public Value(string label)
	{
		this.label = label;
	}

	public Value(string label, T value)
	{
		this.label = label;
		this.value = value;
	}
}

public class Score : Value<int>
{
	public override string type { get; set; } = "Number";

	public int? min { get; set; }

	public int? max { get; set; }

	[JsonProperty("base")]
	public int? baseScore { get; set; }
	
	public int? mod { get; set; }

	public Score(string label, int value) : base(label, value)
	{
	}
}

public class ActorToken {
	
}