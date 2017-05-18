package atmasim.hadoop.mapreduce;

import java.io.File;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.IOException;
import java.nio.file.Paths;
import java.util.Iterator;
import java.util.UUID;
import java.util.Map.Entry;

import org.apache.hadoop.io.Text;
import org.apache.hadoop.mapreduce.Mapper;
import org.apache.log4j.Logger;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

 public class SimMapper  extends Mapper<SimKey, Text, DPSKey, DPSValue>{
    private Logger logger = Logger.getLogger(SimMapper.class);

    @Override 
    public void map(SimKey key, Text value, Context context) throws IOException, InterruptedException {
        String simString = Text.decode(key.simString.getBytes()); // 'time_style_adds_bosses'
        String talentString = Text.decode(key.talentString.getBytes());  //number string of talents
        String reforgeString = Text.decode(value.getBytes()); //c:val,m:val,h:val

        logger.info("Found key: " + key.toString());
        logger.info("Found value: " + reforgeString);

        JsonArray models = ModelProvider.getProvider().models;
        
        String ba = "raid_events+=/adds,count=1,first=30,cooldown=60,duration=20\n";
        String sa = "raid_events+=/adds,count=3,first=45,cooldown=45,duration=10,distance=5\n";
        String doubleTarget = "enemy=enemy2\nraid_events+=/move_enemy,name=enemy2,cooldown=2000,duration=1000,x=-27,y=-27\n";

        String talents = "talents=" + talentString.replace(",", "") + "\n";
        String artifact = "artifact=47:0:0:0:0:764:1:765:1:766:1:767:3:768:1:769:1:770:1:771:3:772:3:773:4:774:3:775:3:776:4:777:4:778:3:779:1:1347:1:1381:1:1573:4:1574:1:1650:1\n";

        String header = "priest=\"Atmasim\"\nlevel=110\nrace=troll\nrole=spell\npriest_ignore_healing=1\nposition=back\n" + talents + artifact + "spec=shadow\ndefault_actions=1\n";
        int threads = 18;

        String crit = reforgeString.split(",")[0];
        String mastery = reforgeString.split(",")[1];
        String haste = reforgeString.split(",")[2];

        String gear = "head=purifiers_gorget,id=138313,bonus_id=3516/1487/1813\nneck=talisman_of_the_shaldorei,id=141325,enchant=600mastery\nshoulders=purifiers_mantle,id=138322,bonus_id=3516/1487/1813\n"
        +"back=purifiers_drape,id=138370,bonus_id=3518/1502/3528,enchant=200int\nchest=soulstitched_robes,id=133611,bonus_id=3418/1808/1542/3528,gems=150haste\nshirt=wraps_of_the_bloodsoaked_brawler,id=98543\ntabard=renowned_guild_tabard,id=69210\n"
        +"wrists=wristbands_of_the_swirling_deeps,id=137372,bonus_id=3418/1557/3337\nhands=scorpid_handlers_gloves,id=140888,bonus_id=3445/1512/3337\nwaist=mangazas_madness,id=132864,bonus_id=3459/3530"
        +"legs=purifiers_leggings,id=138316,bonus_id=3516/1502/3337\nfeet=perpetually_muddy_sandals,id=140854,bonus_id=3445/1502/3336\nfinger1=ring_of_collapsing_futures,id=142173,bonus_id=3418/1808/1517/3337,gems=150haste,enchant=200haste\n"
        +"finger2=sephuzs_secret,id=132452,bonus_id=3529/3530/1811,gems=150haste,enchant=200haste\ntrinket1=unstable_arcanocrystal,id=141482,bonus_id=1472\ntrinket2=brinewater_slime_in_a_bottle,id=142507,bonus_id=3508/605/1512/3528\n"
        +"main_hand=xalatath_blade_of_the_black_empire,id=128827,bonus_id=740,gem_id=140823/140820/140823/0,relic_id=3517:1502:3336/3517:1497:3336/3517:1502:3336/0\noff_hand=secrets_of_the_void,id=133958\n"
        +"scale_to_itemlevel=925\ngear_crit_rating="+ crit + "\ngear_haste_rating="+ haste +"\ngear_mastery_rating="+ mastery +"\nset_bonus=tier19_2pc=0\nset_bonus=tier19_4pc=0\nset_bonus=tier20_2pc=1\nset_bonus=tier20_4pc=1\n";
        String base = "iterations=5000\nthreads="+threads+"\nreport_detail=0\noutput=nul\nmax_time="+ simString.split("_")[0] +"\noptimal_raid=1\nfight_style="+ simString.split("_")[1] +"\nenemy=enemy1\n";

        String addString = "\n";
        if(simString.split("_")[2].equals("ba")) { 
            addString = ba;
        } else if(simString.split("_")[2].equals("sa")) {
            addString = sa;
        }

        String targetString = "\n";
        if(simString.split("_")[3].equals("2t")) {
            targetString = doubleTarget;
        }

        String uuid = UUID.randomUUID().toString();
        String runtimePath = new File(".").getCanonicalPath();
        String profileName = "atmasim-"+uuid;
        String footer = "json2=" + profileName + ".json\n";
        String profileData = header + gear + base + targetString + addString + footer;
        String profilePath = Paths.get(runtimePath, profileName + ".simc").toString();
        File profile = new File(profilePath);
        logger.info("Created profile from key/value data:");
        logger.info("*** START OF PROFILE DATA ***");
        logger.info(profileData);
        logger.info("*** END OF PROFILE DATA ***");
        FileWriter fWriter = new FileWriter(profile);
        fWriter.write(profileData);
        fWriter.close();
        logger.info("Executing profile saved at: " + profilePath);
        SimC.ExecuteSim(profilePath);
        logger.info("SimC finished executing. Collecting results.");
        JsonObject results = new JsonParser().parse(new FileReader(Paths.get(runtimePath, profileName + ".json").toFile())).getAsJsonObject();
        float dps = results.get("sims").getAsJsonObject()
                        .get("players").getAsJsonArray()
                        .get(0).getAsJsonObject()
                        .get("collected_data").getAsJsonObject()
                        .get("dps").getAsJsonObject()
                        .get("mean").getAsFloat();
        logger.info("Found DPS: " + dps);
        for(int i = 0; i <= models.size(); i++) {
            JsonObject model = models.get(i).getAsJsonObject();
            String modelName = model.get("name").toString();
            if(doesModelContainSim(model, simString)) {
                logger.info("Created DPSKey/Value for model: " + modelName);
                context.write(new DPSKey(talentString, modelName), new DPSValue(simString, reforgeString, dps));
            }
        }
        logger.info("Finished mapping key: " + key.toString());
    }
    
    public boolean doesModelContainSim(JsonObject model, String simName) {
        JsonObject sims = model.get("model").getAsJsonObject();
        Iterator<Entry<String,JsonElement>> simKeys = sims.entrySet().iterator();
        logger.info("Checking if model ("+model.get("name")+") contains: " + simName);
        while(simKeys.hasNext()) {
            if (simName.contains(simKeys.next().getKey())) {
                logger.info("Successfully found sim in model.");
                return true;
            }
        }
        logger.info("Did not find sim in model.");
        return false;
    }
 }