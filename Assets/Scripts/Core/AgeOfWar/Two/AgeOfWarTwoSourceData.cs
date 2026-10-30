// Verbatim XML from the original embedded Ressources_DATA_CLASS / Ressources_AI_CLASS.
namespace XanaduYonder.Core.AgeOfWar.Two
{
    internal static class AgeOfWarTwoSourceData
    {
        internal const string Data = @"<ageOfWar>
	<start money=""2500"" xp=""1000"" age=""1"" ageCPU=""1""/>
	<bases>
		<base health=""1000""/>
		<base health=""1500""/>
		<base health=""3000""/>
		<base health=""4000""/>
		<base health=""5000""/>
		<base health=""7500""/>
		<base health=""10000""/>
	</bases>
	<units>
		<!-- Age Préhistorique -->
		<unit name=""Slinger"" cost=""100"" pop=""1"" health=""100"" speed=""2.3"" range=""180"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""15"" damageMax=""30"" attackRate=""Fast"" creationtime=""1"" shootProjectile=""1"" projectileName=""p1"" desc=""Will throw big rocks at the opponents"" scale=""0.75"" hitMeleeSound=""hit4"" hitRangedSound=""swish4"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
			
		<unit name=""Clubman"" cost=""130"" pop=""1"" health=""140"" speed=""1.9"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""25"" damageMax=""35"" attackRate=""Fast"" creationtime=""0.8"" shootProjectile=""0"" projectileName="""" desc=""I will make you join my club!"" scale=""0.75"" hitMeleeSound=""hit4,hit2,hit3"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
			
		<unit name=""Speedy Dino"" cost=""200"" pop=""1"" health=""175"" speed=""3.6"" range=""0"" sizeW=""40"" sizeH=""90"" needUpgrade=""none"" unitType=""Anti-Armor"" damageMin=""30"" damageMax=""40"" attackRate=""Average"" creationtime=""2"" shootProjectile=""0"" projectileName="""" desc=""This unit is the strongest but beware of the slingers"" scale=""0.75"" hitMeleeSound=""sword2"" deathSound=""hurt8""/>
			
		<unit name=""Assault Dino"" cost=""600"" pop=""2"" health=""350"" speed=""1.2"" range=""200"" sizeW=""50"" sizeH=""100"" needUpgrade=""Dino Hut"" unitType=""Armored"" damageMin=""30"" damageMax=""50"" attackRate=""Slow"" creationtime=""4"" shootProjectile=""1"" projectileName=""p2"" desc=""Huge dinosaurs were tamed. They will show you what they have learned!"" scale=""0.8"" hitRangedSound=""swish1,swish2,swish3"" hitMeleeSound=""sword1"" deathSound=""hurt7""/>
		<!-- Age Spartiates -->
		<unit name=""Spear Spartan"" cost=""110"" pop=""1"" health=""110"" speed=""2.3"" range=""170"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""15"" damageMax=""30"" attackRate=""Slow"" creationtime=""1"" shootProjectile=""1"" projectileName=""p3"" desc=""This unit will pierce through you with its spear"" scale=""0.75"" hitMeleeSound=""sword1"" hitRangedSound=""swish1,swish2,swish3"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Sword Spartan"" cost=""140"" pop=""1"" health=""200"" speed=""1.9"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""25"" damageMax=""35"" attackRate=""Normal"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""This unit will cut you in half with its sword"" scale=""0.75"" hitMeleeSound=""sword2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Assault Spartan"" cost=""220"" pop=""1"" health=""160"" speed=""1.5"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Anti-Armor"" damageMin=""25"" damageMax=""35"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""This fearless unit will punish you"" scale=""0.75"" hitMeleeSound=""sword2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Armored Spartan"" cost=""500"" pop=""1"" health=""300"" speed=""4"" range=""0"" sizeW=""30"" sizeH=""75"" needUpgrade=""Armory"" unitType=""Armored"" damageMin=""25"" damageMax=""35"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""This unit benefits from extra armor for better protection"" scale=""0.85"" hitMeleeSound=""sword1"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<!-- Age Egyptien -->
		<unit name=""Priest"" cost=""120"" pop=""1"" health=""150"" speed=""2.3"" range=""200"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""15"" damageMax=""25"" attackRate=""Fast"" creationtime=""1"" shootProjectile=""1"" projectileName=""p4"" desc=""Casts a spell that throws a fireball at the enemy"" scale=""0.75"" hitMeleeSound=""explosion1"" hitRangedSound=""fireball"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Kopesh Warrior"" cost=""150"" pop=""1"" health=""235"" speed=""1.9"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""25"" damageMax=""35"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""Will cut open your throat with his scythe"" scale=""0.75"" hitMeleeSound=""sword1"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Anubis Warrior"" cost=""200"" pop=""1"" health=""350"" speed=""1.9"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Anti-Armor"" damageMin=""30"" damageMax=""50"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""This giant fellow is an expert at manipulating the blades"" scale=""0.75"" hitMeleeSound=""sword2"" deathSound=""hurt7""/>
		<unit name=""Cart Warrior"" cost=""700"" pop=""1"" health=""400"" speed=""1.9"" range=""300"" sizeW=""30"" sizeH=""75"" needUpgrade=""Stables"" unitType=""Armored"" damageMin=""20"" damageMax=""35"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""1"" projectileName=""p5"" desc=""A spear warrior aboard a cart!"" scale=""0.7"" hitMeleeSound=""sword2"" hitRangedSound=""swish1,swish2,swish3"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<!-- Age Medieval -->
		<unit name=""Archer"" cost=""150"" pop=""1"" health=""200"" speed=""2.3"" range=""200"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""15"" damageMax=""30"" attackRate=""Fast"" creationtime=""1"" shootProjectile=""0"" projectileName="""" desc=""This expert with the bow will never miss"" scale=""0.60"" hitMeleeSound=""sword1"" hitRangedSound=""arrow1,arrow2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Footman"" cost=""175"" pop=""1"" health=""300"" speed=""1.5"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""25"" damageMax=""40"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""Will knock everyone out with his spiked club"" scale=""0.65"" hitMeleeSound=""hit2,hit3"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Mage"" cost=""225"" pop=""1"" health=""200"" speed=""1.9"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""Library"" unitType=""Anti-Armor"" damageMin=""50"" damageMax=""60"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""Will hurt the enemy using an electricity spell"" scale=""0.55"" hitMeleeSound=""hit8"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Griffon Knight"" cost=""650"" pop=""1"" health=""400"" speed=""1.9"" range=""0"" sizeW=""50"" sizeH=""75"" needUpgrade=""none"" unitType=""Armored"" damageMin=""40"" damageMax=""50"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""A knight riding a giant griffon will show his feats"" scale=""0.8"" hitMeleeSound=""sword2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<!-- Age Classique -->
		<unit name=""RifleMan"" cost=""150"" pop=""1"" health=""230"" speed=""2.1"" range=""200"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""15"" damageMax=""30"" attackRate=""Fast"" creationtime=""1"" shootProjectile=""0"" projectileName="""" desc=""Will not hesitate to use his musket to teach a lesson"" scale=""0.60"" hitMeleeSound=""sword1"" hitRangedSound=""gun2,gun3"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""SwordMan"" cost=""175"" pop=""1"" health=""350"" speed=""1.9"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""30"" damageMax=""40"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""Handles the sword like nobody else does"" scale=""0.65"" hitMeleeSound=""sword2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Cannon"" cost=""225"" pop=""1"" health=""300"" speed=""1.9"" range=""400"" sizeW=""30"" sizeH=""70"" needUpgrade=""Black Powder"" unitType=""Anti-Armor"" damageMin=""25"" damageMax=""35"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""1"" projectileName=""p6"" desc=""Will roll to victory while firing cannonballs"" scale=""0.6"" hitMeleeSound=""gun9"" hitRangedSound=""gun9"" deathSound=""explosion5""/>
		<unit name=""Knight"" cost=""750"" pop=""1"" health=""800"" speed=""1.7"" range=""0"" sizeW=""30"" sizeH=""75"" needUpgrade=""none"" unitType=""Armored"" damageMin=""50"" damageMax=""70"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""This horse rider will axe his way through"" scale=""0.6"" hitMeleeSound=""sword2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<!-- Age Moderne -->
		<unit name=""Machine-Gun Soldier"" cost=""200"" pop=""1"" health=""600"" speed=""2.3"" range=""150"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""10"" damageMax=""20"" attackRate=""Fast"" creationtime=""1"" shootProjectile=""0"" projectileName="""" desc=""Many times per second, will fire rounds that can penetrate any material"" scale=""0.60"" hitMeleeSound=""gun14"" hitRangedSound=""gun14"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Infantry Soldier"" cost=""150"" pop=""1"" health=""600"" speed=""1.9"" range=""150"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""15"" damageMax=""30"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""0"" projectileName="""" desc=""Will perform avanced combat tactics"" scale=""0.65"" hitMeleeSound=""gun15"" hitRangedSound=""gun15"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Grenade Soldier"" cost=""150"" pop=""1"" health=""600"" speed=""1.9"" range=""300"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Anti-Armor"" damageMin=""20"" damageMax=""40"" attackRate=""Fast"" creationtime=""0.6"" shootProjectile=""1"" projectileName=""p7"" desc=""Will blow, blow-up rather, your mind"" scale=""0.6"" hitMeleeSound=""sword2"" hitRangedSound=""swish1,swish2"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Tank"" cost=""800"" pop=""2"" health=""800"" speed=""1.9"" range=""350"" sizeW=""50"" sizeH=""75"" needUpgrade=""Factories"" unitType=""Armored"" damageMin=""40"" damageMax=""60"" attackRate=""Fast"" creationtime=""4"" shootProjectile=""0"" projectileName="""" desc=""Will steamroll everything that stands in its way"" scale=""1.3"" hitMeleeSound=""gun7"" hitRangedSound=""gun4"" deathSound=""explosion5""/>
		<!-- Age Futuriste -->
		<unit name=""Cyborg"" cost=""250"" pop=""1"" health=""850"" speed=""2"" range=""250"" sizeW=""30"" sizeH=""80"" needUpgrade=""none"" unitType=""Support"" damageMin=""15"" damageMax=""30"" attackRate=""Fast"" creationtime=""2"" shootProjectile=""0"" projectileName="""" desc=""Will outsmart and outgun you every single time"" scale=""0.60"" hitMeleeSound=""gun12"" hitRangedSound=""gun12"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Spider Blade"" cost=""200"" pop=""1"" health=""1000"" speed=""4"" range=""0"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Infantry"" damageMin=""50"" damageMax=""60"" attackRate=""Average"" creationtime=""1"" shootProjectile=""0"" projectileName="""" desc=""Not really human anymore. Fast and deadly at close range."" scale=""0.65"" hitMeleeSound=""hit9"" deathSound=""hurt10""/>
		<unit name=""Mad Scientist"" cost=""250"" pop=""1"" health=""800"" speed=""1.2"" range=""400"" sizeW=""30"" sizeH=""70"" needUpgrade=""none"" unitType=""Anti-Armor"" damageMin=""25"" damageMax=""35"" attackRate=""Fast"" creationtime=""3"" shootProjectile=""0"" projectileName="""" desc=""Will help you take care of heavy units like the ACS"" scale=""0.6"" hitMeleeSound=""sword2"" hitRangedSound=""gun5"" deathSound=""hurt1,hurt2,hurt3,hurt4,hurt5,hurt6""/>
		<unit name=""Armored Combat Suit"" cost=""500"" pop=""1"" health=""1200"" speed=""1.2"" range=""200"" sizeW=""45"" sizeH=""75"" needUpgrade=""none"" unitType=""Armored"" damageMin=""20"" damageMax=""35"" attackRate=""Very Fast"" creationtime=""3"" shootProjectile=""0"" projectileName="""" desc=""The ACS is a killer in the frontlines"" scale=""0.7"" hitMeleeSound=""gun13"" hitRangedSound=""gun13"" deathSound=""hurt9""/>
		<unit name=""God's Wrath"" cost=""10000"" pop=""2"" health=""15000"" speed=""1"" range=""0"" sizeW=""50"" sizeH=""100"" needUpgrade=""Power Coil"" unitType=""Special"" damageMin=""600"" damageMax=""1200"" attackRate=""Slow"" creationtime=""6"" shootProjectile=""0"" projectileName="""" desc=""Unleash the strongest war machine in human history"" scale=""0.5"" hitMeleeSound=""gun10"" deathSound=""hurt9""/>
	</units>
	<projectiles>
		<!-- random, follow or a number -->
		<projectile name=""p1"" size=""0.7"" rotSpeed=""random"" mass=""20"" hitSound=""hit4""/>
		<projectile name=""p2"" size=""0.75"" rotSpeed=""7"" mass=""10"" hitSound=""sword2""/>
		<projectile name=""p3"" size=""0.75"" rotSpeed=""follow"" mass=""10"" hitSound=""sword2""/>
		<projectile name=""p4"" size=""1"" rotSpeed=""follow"" mass=""20"" explosion=""0.75""/>
		<projectile name=""p5"" size=""0.75"" rotSpeed=""follow"" mass=""10"" hitSound=""sword2""/>
		<projectile name=""p6"" size=""1"" rotSpeed=""follow"" mass=""10""/>
		<projectile name=""p7"" size=""1"" rotSpeed=""random"" mass=""10"" explosion=""0.5""/>
		<projectile name=""tp1"" size=""1.2"" rotSpeed=""7"" mass=""10"" hitSound=""hit4""/>
		<projectile name=""tp2"" size=""0.8"" rotSpeed=""7"" mass=""10""/>
		<projectile name=""tp3"" size=""1.1"" rotSpeed=""7"" mass=""10"" hitSound=""hit4""/>
		<projectile name=""tp4"" size=""1"" rotSpeed=""follow"" mass=""10"" hitSound=""sword2""/>
		<projectile name=""tp5"" size=""1"" rotSpeed=""follow"" mass=""10"" explosion=""0.5""/>
		<projectile name=""tp6"" size=""1"" rotSpeed=""follow"" mass=""10"" explosion=""0.7""/>
		<projectile name=""tp7"" size=""0.8"" rotSpeed=""follow"" mass=""10"" hitSound=""sword2""/>
		<projectile name=""tp8"" size=""1"" rotSpeed=""random"" mass=""10"" hitSound=""hit4""/>
		<projectile name=""tp9"" size=""1"" rotSpeed=""random"" mass=""10"" hitSound=""hit4""/>
		<projectile name=""tp10"" size=""1"" rotSpeed=""follow"" mass=""10"" hitSound=""hit4""/>
		<projectile name=""tp11"" size=""1"" rotSpeed=""follow"" mass=""10"" hitSound=""hit4""/>
		<projectile name=""tp12"" size=""1"" rotSpeed=""follow"" mass=""10""/>
		<projectile name=""tp13"" size=""1"" rotSpeed=""follow"" mass=""10"" explosion=""1""/>
		<projectile name=""tp14"" size=""1"" rotSpeed=""follow"" mass=""10""/>
		<projectile name=""tp15"" size=""1"" rotSpeed=""follow"" mass=""10"" explosion=""1""/>
	</projectiles>
	<turrets>
		<turret name=""Mamooth Catapult"" cost=""750"" range=""400"" damageMin=""8"" damageMax=""16"" shootProjectile=""1"" projectileName=""tp1"" desc=""This ivory-based device will throw rocks at the enemy"" scale=""1"" shootSound=""swish4""/>
		<turret name=""Egg Rifle"" cost=""1500"" range=""450"" damageMin=""7"" damageMax=""14"" shootProjectile=""1"" projectileName=""tp2"" desc=""An enraged hen will catapult eggs at the enemy"" scale=""1""/>
		<turret name=""Rock Slingshot"" cost=""2300"" range=""300"" damageMin=""12"" damageMax=""20"" shootProjectile=""1"" projectileName=""tp3"" desc=""This elastic-powered rock propeller will certainly hurt"" scale=""1"" shootSound=""swish4""/>
		<turret name=""Guard"" cost=""2000"" range=""300"" damageMin=""16"" damageMax=""24"" shootProjectile=""1"" projectileName=""tp4"" desc=""This alert guard will never faint"" scale=""1"" shootSound=""swish1,swish2,swish3""/>
		<turret name=""Golden Eagle"" cost=""1500"" range=""450"" damageMin=""15"" damageMax=""30"" shootProjectile=""1"" projectileName=""tp5"" desc=""This royal eagle will fire fireballs at will"" scale=""1"" shootSound=""rocketLaunch""/>
		<turret name=""Big Bird"" cost=""2300"" range=""300"" damageMin=""20"" damageMax=""35"" shootProjectile=""1"" projectileName=""tp6"" desc=""This bird will fire strong projectiles towards you"" scale=""1"" shootSound=""rocketLaunch""/>
		<turret name=""Crossbow"" cost=""1000"" range=""300"" damageMin=""25"" damageMax=""45"" shootProjectile=""1"" projectileName=""tp7"" desc=""This crossbow will fire powerful arrows"" scale=""1"" shootSound=""swish1,swish2,swish3""/>
		<turret name=""Rock Catapult"" cost=""2000"" range=""300"" damageMin=""40"" damageMax=""60"" shootProjectile=""1"" projectileName=""tp8"" desc=""This catapult will fire rocks"" scale=""1"" shootSound=""swish4""/>
		<turret name=""Metal Catapult"" cost=""3000"" range=""300"" damageMin=""50"" damageMax=""70"" shootProjectile=""1"" projectileName=""tp9"" desc=""This catapult will fire metal balls"" scale=""1"" shootSound=""swish4""/>
		<turret name=""Small Cannon"" cost=""2000"" range=""300"" damageMin=""55"" damageMax=""75"" shootProjectile=""1"" projectileName=""tp10"" desc=""This cannon will fire small cannonballs"" scale=""1"" shootSound=""gun4""/>
		<turret name=""Large Cannon"" cost=""4000"" range=""300"" damageMin=""65"" damageMax=""80"" shootProjectile=""1"" projectileName=""tp11"" desc=""This cannon will fire big cannonballs"" scale=""1"" shootSound=""gun9""/>
		<turret name=""MachineGun"" cost=""2000"" range=""300"" damageMin=""10"" damageMax=""14"" shootProjectile=""1"" projectileName=""tp12"" desc=""This MG will rapidly fire many rounds at the enemy"" scale=""1"" shootSound=""gun5""/>
		<turret name=""Rocket Launcher"" cost=""3000"" range=""400"" damageMin=""70"" damageMax=""90"" shootProjectile=""1"" projectileName=""tp13"" desc=""This RL will fire huge missiles at the enemy"" scale=""1"" shootSound=""rocketLaunch""/>
		<turret name=""Double Rocket Launcher"" cost=""4500"" range=""300"" damageMin=""70"" damageMax=""90"" shootProjectile=""1"" projectileName=""tp13"" desc=""This is the combination of two rocket launchers in one tower spot"" scale=""1"" shootSound=""rocketLaunch""/>
		<turret name=""Ion Cannon"" cost=""3000"" range=""400"" damageMin=""15"" damageMax=""20"" shootProjectile=""1"" projectileName=""tp14"" desc=""This ion-emitting device will devastate the enemy "" scale=""1"" shootSound=""gun11""/>
		<turret name=""Plasma Cannon"" cost=""5000"" range=""450"" damageMin=""70"" damageMax=""120"" shootProjectile=""1"" projectileName=""tp15"" desc=""This plasma cannon will fire plasma rounds"" scale=""1"" shootSound=""gun12""/>
		<turret name=""Heavy Plasma Cannon"" cost=""7000"" range=""400"" damageMin=""60"" damageMax=""170"" shootProjectile=""1"" projectileName=""tp15"" desc=""This heavy plasma cannon will fire concentrated plasma balls"" scale=""1"" shootSound=""gun13""/>
	</turrets>
	<upgrades>
		<upgrade name=""Dino Hut"" level=""0"">
			<level name=""Dino Hut"" cost=""300"" desc=""Builds a training hut for bigger dinosaurs (required for the Assault Dino)""/>
			<level name=""Dino Hut Built"" cost="""" desc=""You can now train Assault Dinos""/>
		</upgrade>
		<upgrade name=""Armory"" level=""0"">
			<level name=""Armory"" cost=""400"" desc=""Unlocks better armors (required for the Armored Spartan)""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can now train Armored Spartans""/>
		</upgrade>
		<upgrade name=""Stables"" level=""0"">
			<level name=""Stables"" cost=""500"" desc=""Builds a stable to train horses (required for the Cart Warrior)""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can now train Cart Warriors""/>
		</upgrade>
		<upgrade name=""Library"" level=""0"">
			<level name=""Library"" cost=""600"" desc=""Builds a library (required for the Mage)""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can now train Mages""/>
		</upgrade>
		<upgrade name=""Black Powder"" level=""0"">
			<level name=""Black Powder"" cost=""800"" desc=""Researches black powder (needed for the Cannon)""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can now build Cannons""/>
		</upgrade>
		<upgrade name=""Factories"" level=""0"">
			<level name=""Factories"" cost=""1000"" desc=""Builds a factory (required for the Tank)""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can now build Tanks""/>
		</upgrade>
		<upgrade name=""Power Coil"" level=""0"">
			<level name=""Power Coil"" cost=""5000"" desc=""Build a power coil (required for the God's Wrath)""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can now build God's Wraths""/>
		</upgrade>
		<upgrade name=""Income"" level=""0"">
			<level name=""Income level 1"" cost=""500"" desc=""Get more money from killed enemy units &#10;&#10;-Current level: 100% of unit value &#10;-Next level: 110% of unit value"" bonus=""0""/>
			<level name=""Income level 2"" cost=""1100"" desc=""Get more money from killed enemy units &#10;&#10;-Current level: 110% of unit value &#10;-Next level: 120% of unit value"" bonus=""0.1""/>
			<level name=""Income level 3"" cost=""2000"" desc=""Get more money from killed enemy units &#10;&#10;-Current level: 120% of unit value &#10;-Next level: 130% of unit value"" bonus=""0.2""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer increase your income level"" bonus=""0.3""/>
		</upgrade>
		<upgrade name=""Support Damage"" level=""0"">
			<level name=""Support Damage"" cost=""300"" desc=""Upgrades the damage of your support units"" bonus=""0""/>
			<level name=""Support Damage"" cost=""1500"" desc=""Upgrades the damage of your support units"" bonus=""0.2""/>
			<level name=""Support Damage"" cost=""3500"" desc=""Upgrades the damage of your support units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the damage of your support units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Support Range"" level=""0"">
			<level name=""Support Range"" cost=""300"" desc=""Increases the range of your support units"" bonus=""0""/>
			<level name=""Support Range"" cost=""700"" desc=""Increases the range of your support units"" bonus=""0.2""/>
			<level name=""Support Range"" cost=""1500"" desc=""Increases the range of your support units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer increase the range of your support units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Infantry Damage"" level=""0"">
			<level name=""Infantry Damage"" cost=""300"" desc=""Upgrades the damage of your infantry units"" bonus=""0""/>
			<level name=""Infantry Damage"" cost=""1700"" desc=""Upgrades the damage of your infantry units"" bonus=""0.2""/>
			<level name=""Infantry Damage"" cost=""3500"" desc=""Upgrades the damage of your infantry units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the damage of your infantry units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Infantry Armor"" level=""0"">
			<level name=""Infantry Armor"" cost=""300"" desc=""Upgrades the armor of your infantry units"" bonus=""0""/>
			<level name=""Infantry Armor"" cost=""1700"" desc=""Upgrades the armor of your infantry units"" bonus=""0.2""/>
			<level name=""Infantry Armor"" cost=""3500"" desc=""Upgrades the armor of your infantry units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the armor of your infantry units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Anti-Armor Damage"" level=""0"">
			<level name=""Anti-Armor Damage"" cost=""400"" desc=""Upgrades the damage of your anti-armor units"" bonus=""0""/>
			<level name=""Anti-Armor Damage"" cost=""800"" desc=""Upgrades the damage of your anti-armor units"" bonus=""0.2""/>
			<level name=""Anti-Armor Damage"" cost=""2600"" desc=""Upgrades the damage of your anti-armor units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the damage of your anti-armor units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Anti-Armor Armor"" level=""0"">
			<level name=""Anti-Armor Armor"" cost=""300"" desc=""Upgrades the armor of your anti-armor units"" bonus=""0""/>
			<level name=""Anti-Armor Armor"" cost=""1700"" desc=""Upgrades the armor of your anti-armor units"" bonus=""0.2""/>
			<level name=""Anti-Armor Armor"" cost=""3500"" desc=""Upgrades the armor of your anti-armor units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the armor of your anti-armor units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Armored Damage"" level=""0"">
			<level name=""Armored Damage"" cost=""300"" desc=""Upgrades the damage of your armored units"" bonus=""0""/>
			<level name=""Armored Damage"" cost=""1700"" desc=""Upgrades the damage of your armored units"" bonus=""0.2""/>
			<level name=""Armored Damage"" cost=""3500"" desc=""Upgrades the damage of your armored units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the damage of your armored units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Armored Armor"" level=""0"">
			<level name=""Armored Armor"" cost=""300"" desc=""Upgrades the armor of your armored units"" bonus=""0""/>
			<level name=""Armored Armor"" cost=""1700"" desc=""Upgrades the armor of your armored units"" bonus=""0.2""/>
			<level name=""Armored Armor"" cost=""3500"" desc=""Upgrades the armor of your armored units"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the armor of your armored units"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Turrets Damage"" level=""0"">
			<level name=""Turrets Damage"" cost=""300"" desc=""Upgrades the damage of your turrets"" bonus=""0""/>
			<level name=""Turrets Damage"" cost=""1700"" desc=""Upgrades the damage of your turrets"" bonus=""0.2""/>
			<level name=""Turrets Damage"" cost=""3500"" desc=""Upgrades the damage of your turrets"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer upgrade the damage of your turrets"" bonus=""1""/>
		</upgrade>
		<upgrade name=""Turrets Range"" level=""0"">
			<level name=""Turrets Range"" cost=""300"" desc=""Increases the range of your turrets"" bonus=""0""/>
			<level name=""Turrets Range"" cost=""1700"" desc=""Increases the range of your turrets"" bonus=""0.2""/>
			<level name=""Turrets Range"" cost=""3500"" desc=""Increases the range of your turrets"" bonus=""0.5""/>
			<level name=""Upgrade Done"" cost="""" desc=""You can no longer increase the range of your turrets"" bonus=""1""/>
		</upgrade>
		
	</upgrades>
	<towers>
		<tower name=""Build a defense tower"" cost=""250"" desc=""Build a tower to defend yourself against attacks""/>
		<tower name=""Upgrade your tower"" cost=""1000"" desc=""Raise the height of your defense tower""/>
		<tower name=""Upgrade your tower"" cost=""3000"" desc=""Raise the height of your defense tower further""/>
		<tower name=""Complete your tower"" cost=""7000"" desc=""Bring your tower to its maximum height!""/>
	</towers>
	<evolutions>
		<evolution name=""Evolve to Spartan Age"" cost=""7000"" desc=""Upgrades your armor and weapons""/>
		<evolution name=""Evolve to Egyptian Age"" cost=""8000"" desc=""Strengthens your equipment to endure higher temperatures""/>
		<evolution name=""Evolve to Medieval Age"" cost=""9000"" desc=""Researches catapults and ballistas""/>
		<evolution name=""Evolve to Renaissance Age"" cost=""10000"" desc=""Researches gunpowder""/>
		<evolution name=""Evolve to Modern Age"" cost=""11000"" desc=""Researches modern military weaponry""/>
		<evolution name=""Evolve to Futuristic Age"" cost=""12000"" desc=""Researches lasers and perfects cloning""/>
		<evolution name=""Cannot evolve anymore"" cost="""" desc=""You have reached the summit in technology development""/>
	</evolutions>
	
	<specials>
		<special name=""Meteorite Rain"" cost=""2000"" desc=""Launches an avalanche of meteorites upon your enemy""/>
		<special name=""Zeus' Anger"" cost=""2500"" desc=""The mighty god Zeus unleashes his anger in the form of a thunderstorm""/>
		<special name=""Rage of the Desert"" cost=""3000"" desc=""Strikes your enemy with powerful missiles""/>
		<special name=""Arrow Storm"" cost=""4000"" desc=""The sky gets flooded with your annihilating arrows""/>
		<special name=""Unhappy King"" cost=""5000"" desc=""Explosive rocks get catapulted all over the place""/>
		<special name=""Special Cargo"" cost=""6000"" desc=""Nukes the terrain with bombshells""/>
		<special name=""Death Ray"" cost=""7000"" desc=""A powerful laserbeam reduces enemy units to dust""/>
	</specials>
</ageOfWar>";
        internal const string Ai = @"<ageOfWar>
	<ai>
		<age>
			<event duration=""20"">
				<units min=""2"" max=""5"">
					<unit id=""1"" ratio=""0.5""/>
					<unit id=""2"" ratio=""0.5""/>
				</units>
				<build>
					<tower/>
					<turret spot=""1"" id=""1""/>
				</build>
				<sell>
				</sell>
			</event>
			
			<event duration=""30"">
				<units min=""2"" max=""5"">
					<unit id=""1"" ratio=""0.4""/>
					<unit id=""2"" ratio=""0.4""/>
					<unit id=""3"" ratio=""0.2""/>
				</units>
				<build>
					<special/>
				</build>
				<sell>
				</sell>
			</event>
			
			<event duration=""40"">
				<units min=""3"" max=""6"">
					<unit id=""1"" ratio=""0.333""/>
					<unit id=""2"" ratio=""0.333""/>
					<unit id=""3"" ratio=""0.333""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			
			<event duration=""75"">
				<units min=""4"" max=""6"">
					<unit id=""1"" ratio=""0.2""/>
					<unit id=""2"" ratio=""0.3""/>
					<unit id=""3"" ratio=""0.3""/>
					<unit id=""4"" ratio=""0.2""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
		</age>
		<age>
			<event duration=""25"">
				<units min=""3"" max=""6"">
					<unit id=""5"" ratio=""0.7""/>
					<unit id=""6"" ratio=""0.3""/>
				</units>
				<build>
					<tower/>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""30"">
				<units min=""4"" max=""8"">
					<unit id=""5"" ratio=""0.4""/>
					<unit id=""6"" ratio=""0.4""/>
					<unit id=""7"" ratio=""0.2""/>
				</units>
				<build>
					<turret spot=""2"" id=""4""/>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""3"" max=""6"">
					<unit id=""5"" ratio=""0.333""/>
					<unit id=""6"" ratio=""0.333""/>
					<unit id=""7"" ratio=""0.333""/>
				</units>
				<build>
					<special/>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""5"" max=""8"">
					<unit id=""6"" ratio=""0.3""/>
					<unit id=""7"" ratio=""0.7""/>
				</units>
				<build>
				</build>
				<sell>
					<turret spot=""1""/>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""3"" max=""6"">
					<unit id=""5"" ratio=""0.2""/>
					<unit id=""6"" ratio=""0.2""/>
					<unit id=""7"" ratio=""0.2""/>
					<unit id=""8"" ratio=""0.4""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
		</age>
		<age>
			<event duration=""45"">
				<units min=""3"" max=""6"">
					<unit id=""10"" ratio=""1""/>
				</units>
				<build>
					<turret spot=""1"" id=""5""/>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""80"">
				<units min=""4"" max=""8"">
					<unit id=""9"" ratio=""0.4""/>
					<unit id=""10"" ratio=""0.4""/>
					<unit id=""11"" ratio=""0.2""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""3"" max=""6"">
					<unit id=""9"" ratio=""0.333""/>
					<unit id=""10"" ratio=""0.333""/>
					<unit id=""12"" ratio=""0.333""/>
				</units>
				<build>
					<turret spot=""2"" id=""5""/>
					<special/>
				</build>
				<sell>
					<turret spot=""2""/>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""5"" max=""8"">
					<unit id=""12"" ratio=""0.3""/>
					<unit id=""9"" ratio=""0.7""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""3"" max=""6"">
					<unit id=""9"" ratio=""0.2""/>
					<unit id=""10"" ratio=""0.2""/>
					<unit id=""11"" ratio=""0.2""/>
					<unit id=""12"" ratio=""0.4""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
		</age>
		<age>
			<event duration=""45"">
				<units min=""3"" max=""6"">
					<unit id=""13"" ratio=""1""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""80"">
				<units min=""4"" max=""8"">
					<unit id=""13"" ratio=""0.4""/>
					<unit id=""14"" ratio=""0.4""/>
					<unit id=""15"" ratio=""0.2""/>
				</units>
				<build>
					<turret spot=""1"" id=""7""/>
				</build>
				<sell>
					<turret spot=""1""/>
				</sell>
			</event>
			<event duration=""100"">
				<units min=""3"" max=""6"">
					<unit id=""13"" ratio=""0.333""/>
					<unit id=""16"" ratio=""0.333""/>
					<unit id=""15"" ratio=""0.333""/>
				</units>
				<build>
					<turret spot=""2"" id=""8""/>
					<special/>
				</build>
				<sell>
					<turret spot=""2""/>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""5"" max=""8"">
					<unit id=""14"" ratio=""0.3""/>
					<unit id=""13"" ratio=""0.7""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""50"">
				<units min=""3"" max=""6"">
					<unit id=""13"" ratio=""0.2""/>
					<unit id=""14"" ratio=""0.2""/>
					<unit id=""15"" ratio=""0.2""/>
					<unit id=""16"" ratio=""0.4""/>
				</units>
				<build>
					<tower/>
				</build>
				<sell>
				</sell>
			</event>
		</age>
		<age>
			<event duration=""45"">
				<units min=""3"" max=""6"">
					<unit id=""17"" ratio=""1""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""40"">
				<units min=""4"" max=""8"">
					<unit id=""17"" ratio=""0.4""/>
					<unit id=""18"" ratio=""0.4""/>
					<unit id=""19"" ratio=""0.2""/>
				</units>
				<build>
					<turret spot=""1"" id=""10""/>
				</build>
				<sell>
					<turret spot=""1""/>
				</sell>
			</event>
			<event duration=""80"">
				<units min=""3"" max=""6"">
					<unit id=""17"" ratio=""0.333""/>
					<unit id=""18"" ratio=""0.333""/>
					<unit id=""19"" ratio=""0.333""/>
				</units>
				<build>
					<turret spot=""2"" id=""11""/>
					<special/>
				</build>
				<sell>
					<turret spot=""2""/>
				</sell>
			</event>
			<event duration=""70"">
				<units min=""3"" max=""6"">
					<unit id=""17"" ratio=""0.2""/>
					<unit id=""18"" ratio=""0.2""/>
					<unit id=""19"" ratio=""0.2""/>
					<unit id=""20"" ratio=""0.4""/>
				</units>
				<build>
				<turret spot=""3"" id=""10""/>
				</build>
				<sell>
				</sell>
			</event>
		</age>
		<age>
			<event duration=""45"">
				<units min=""3"" max=""6"">
					<unit id=""21"" ratio=""1""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""40"">
				<units min=""4"" max=""8"">
					<unit id=""21"" ratio=""0.4""/>
					<unit id=""22"" ratio=""0.4""/>
					<unit id=""23"" ratio=""0.2""/>
				</units>
				<build>
					<turret spot=""1"" id=""12""/>
				</build>
				<sell>
					<turret spot=""1""/>
				</sell>
			</event>
			<event duration=""80"">
				<units min=""3"" max=""6"">
					<unit id=""21"" ratio=""0.333""/>
					<unit id=""22"" ratio=""0.333""/>
					<unit id=""24"" ratio=""0.333""/>
				</units>
				<build>
					<turret spot=""2"" id=""13""/>
					<special/>
				</build>
				<sell>
					<turret spot=""2""/>
				</sell>
			</event>
			<event duration=""70"">
				<units min=""3"" max=""6"">
					<unit id=""21"" ratio=""0.2""/>
					<unit id=""22"" ratio=""0.4""/>
					<unit id=""23"" ratio=""0.2""/>
					<unit id=""24"" ratio=""0.2""/>
				</units>
				<build>
					<turret spot=""3"" id=""14""/>
				</build>
				<sell>
					<turret spot=""3""/>
				</sell>
			</event>
		</age>
		<age>
			<event duration=""45"">
				<units min=""3"" max=""6"">
					<unit id=""26"" ratio=""1""/>
				</units>
				<build>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""40"">
				<units min=""4"" max=""8"">
					<unit id=""25"" ratio=""0.4""/>
					<unit id=""26"" ratio=""0.4""/>
					<unit id=""27"" ratio=""0.2""/>
				</units>
				<build>
					<turret spot=""1"" id=""15""/>
				</build>
				<sell>
					<turret spot=""1""/>
				</sell>
			</event>
			<event duration=""80"">
				<units min=""3"" max=""6"">
					<unit id=""25"" ratio=""0.333""/>
					<unit id=""27"" ratio=""0.333""/>
					<unit id=""28"" ratio=""0.333""/>
				</units>
				<build>
					<turret spot=""2"" id=""16""/>
					<special/>
				</build>
				<sell>
					<turret spot=""2""/>
				</sell>
			</event>
			<event duration=""70"">
				<units min=""3"" max=""6"">
					<unit id=""25"" ratio=""0.2""/>
					<unit id=""26"" ratio=""0.4""/>
					<unit id=""27"" ratio=""0.2""/>
					<unit id=""28"" ratio=""0.2""/>
				</units>
				<build>
					<tower/>
					<turret spot=""3"" id=""17""/>
				</build>
				<sell>
					<turret spot=""3""/>
				</sell>
			</event>
			<event duration=""70"">
				<units min=""3"" max=""6"">
					<unit id=""25"" ratio=""0.25""/>
					<unit id=""26"" ratio=""0.25""/>
					<unit id=""27"" ratio=""0.2""/>
					<unit id=""28"" ratio=""0.3""/>
				</units>
				<build>
					<turret spot=""4"" id=""17""/>
				</build>
				<sell>
				</sell>
			</event>
			<event duration=""70"">
				<units min=""3"" max=""6"">
					<unit id=""25"" ratio=""0.25""/>
					<unit id=""26"" ratio=""0.25""/>
					<unit id=""27"" ratio=""0.2""/>
					<unit id=""28"" ratio=""0.3""/>
				</units>
				<build>
					<turret spot=""1"" id=""17""/>
				</build>
				<sell>
					<turret spot=""1""/>
				</sell>
			</event>
		</age>
	</ai>
</ageOfWar>";
    }
}
