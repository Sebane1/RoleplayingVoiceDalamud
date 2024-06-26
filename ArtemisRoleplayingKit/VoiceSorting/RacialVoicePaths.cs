﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud {
    public class RacialVoicePaths {
        private static int[] emoteVoiceValues = { 7453000, 7452000, 7451000, 7403000, 7402000, 7401000, 7353000, 7352000, 7351000,
            7303000, 7302000, 7301000, 7456000, 7455000, 7454000, 7406000, 7405000, 7404000, 7356000, 7355000, 7354000, 7306000,
            7305000, 7304000, 7471000, 7470000, 7469000, 7421000, 7420000, 7419000, 7371000, 7370000, 7369000, 7321000, 7320000, 
            7319000, 7474000, 7473000, 7472000, 7424000, 7423000, 7422000, 7374000, 7373000, 7372000, 7324000, 7323000, 7322000, 
            7465000, 7464000, 7463000, 7415000, 7414000, 7413000, 7365000, 7364000, 7363000, 7315000, 7314000, 7313000, 7468000, 
            7467000, 7466000, 7418000, 7417000, 7416000, 7368000, 7367000, 7366000, 7318000, 7317000, 7316000, 7459000, 7458000, 
            7457000, 7409000, 7408000, 7407000, 7359000, 7358000, 7357000, 7309000, 7308000, 7307000, 7462000, 7461000, 7460000,
            7412000, 7411000, 7410000, 7362000, 7361000, 7360000, 7312000, 7311000, 7310000, 7477000, 7476000, 7475000, 7427000,
            7426000, 7425000, 7377000, 7376000, 7375000, 7327000, 7326000, 7325000, 7480000, 7479000, 7478000, 7430000, 7429000, 
            7428000, 7380000, 7379000, 7378000, 7330000, 7329000, 7328000, 7483000, 7482000, 7481000, 7433000, 7432000, 7431000,
            7383000, 7382000, 7381000, 7333000, 7332000, 7331000, 7486000, 7485000, 7484000, 7436000, 7435000, 7434000, 7386000,
            7385000, 7384000, 7336000, 7335000, 7334000, 7489000, 7488000, 7487000, 7439000, 7438000, 7437000, 7389000, 7388000, 
            7387000, 7339000, 7338000, 7337000, 7492000, 7491000, 7490000, 7442000, 7441000, 7440000, 7392000, 7391000, 7390000, 
            7342000, 7341000, 7340000, 17453000, 17452000, 17451000, 17403000, 17402000, 17401000, 17353000, 17352000, 17351000,
            17303000, 17302000, 17301000, 17459000, 17458000, 17457000, 17409000, 17408000, 17407000, 17359000, 17358000,
            17357000, 17309000, 17308000, 17307000, 17462000, 17461000, 17460000, 17412000, 17411000, 17410000, 17362000, 
            17361000, 17360000, 17312000, 17311000, 17310000, };

        private static string[] battleValues = { "vo_battle_pc_mid_mb_ja", "vo_battle_pc_mid_mb_en", "vo_battle_pc_mid_mb_de", 
            "vo_battle_pc_mid_mb_fr", "vo_battle_pc_mid_fa_ja", "vo_battle_pc_mid_fb_ja", 
            "vo_battle_pc_mid_fa_en", "vo_battle_pc_mid_fb_en", "vo_battle_pc_mid_fa_de", 
            "vo_battle_pc_mid_fb_de", "vo_battle_pc_mid_fa_fr", "vo_battle_pc_mid_fb_fr", 
            "vo_battle_pc_hil_mb_ja", "vo_battle_pc_hil_mb_en", "vo_battle_pc_hil_mb_de", 
            "vo_battle_pc_hil_mb_fr", "vo_battle_pc_hil_fb_ja", "vo_battle_pc_hil_fb_en",
            "vo_battle_pc_hil_fb_de", "vo_battle_pc_hil_fb_fr", "vo_battle_pc_ele_ma_ja", 
            "vo_battle_pc_ele_mb_ja", "vo_battle_pc_ele_ma_en", "vo_battle_pc_ele_mb_en", 
            "vo_battle_pc_ele_ma_de", "vo_battle_pc_ele_mb_de", "vo_battle_pc_ele_ma_fr", 
            "vo_battle_pc_ele_mb_fr", "vo_battle_pc_ele_fb_ja", "vo_battle_pc_ele_fb_en", 
            "vo_battle_pc_ele_fb_de", "vo_battle_pc_ele_fb_fr", "vo_battle_pc_lal_ma_ja",
            "vo_battle_pc_lal_mb_ja", "vo_battle_pc_lal_ma_en", "vo_battle_pc_lal_mb_en", 
            "vo_battle_pc_lal_ma_de", "vo_battle_pc_lal_mb_de", "vo_battle_pc_lal_ma_fr",
            "vo_battle_pc_lal_mb_fr", "vo_battle_pc_lal_fa_ja", "vo_battle_pc_ele_fa_ja",
            "vo_battle_pc_lal_fa_en", "vo_battle_pc_ele_fa_en", "vo_battle_pc_lal_fa_de", 
            "vo_battle_pc_ele_fa_de", "vo_battle_pc_lal_fa_fr", "vo_battle_pc_ele_fa_fr", 
            "vo_battle_pc_miq_ma_ja", "vo_battle_pc_miq_mb_ja", "vo_battle_pc_mid_ma_ja",
            "vo_battle_pc_miq_ma_en", "vo_battle_pc_miq_mb_en", "vo_battle_pc_mid_ma_en", 
            "vo_battle_pc_miq_ma_de", "vo_battle_pc_miq_mb_de", "vo_battle_pc_mid_ma_de",
            "vo_battle_pc_miq_ma_fr", "vo_battle_pc_miq_mb_fr", "vo_battle_pc_mid_ma_fr",
            "vo_battle_pc_miq_fa_ja", "vo_battle_pc_miq_fb_ja", "vo_battle_pc_lal_fb_ja",
            "vo_battle_pc_miq_fa_en", "vo_battle_pc_miq_fb_en", "vo_battle_pc_lal_fb_en", 
            "vo_battle_pc_miq_fa_de", "vo_battle_pc_miq_fb_de", "vo_battle_pc_lal_fb_de", 
            "vo_battle_pc_miq_fa_fr", "vo_battle_pc_miq_fb_fr", "vo_battle_pc_lal_fb_fr", 
            "vo_battle_pc_rog_ma_ja", "vo_battle_pc_rog_mb_ja", "vo_battle_pc_hil_ma_ja", 
            "vo_battle_pc_rog_ma_en", "vo_battle_pc_rog_mb_en", "vo_battle_pc_hil_ma_en", 
            "vo_battle_pc_rog_ma_de", "vo_battle_pc_rog_mb_de", "vo_battle_pc_hil_ma_de",
            "vo_battle_pc_rog_ma_fr", "vo_battle_pc_rog_mb_fr", "vo_battle_pc_hil_ma_fr",
            "vo_battle_pc_rog_fa_ja", "vo_battle_pc_rog_fb_ja", "vo_battle_pc_hil_fa_ja",
            "vo_battle_pc_rog_fa_en", "vo_battle_pc_rog_fb_en", "vo_battle_pc_hil_fa_en", 
            "vo_battle_pc_rog_fa_de", "vo_battle_pc_rog_fb_de", "vo_battle_pc_hil_fa_de", 
            "vo_battle_pc_rog_fa_fr", "vo_battle_pc_rog_fb_fr", "vo_battle_pc_hil_fa_fr", 
            "vo_battle_pc_aur_ma_ja", "vo_battle_pc_aur_mb_ja", "vo_battle_pc_aur_mc_ja",
            "vo_battle_pc_aur_ma_en", "vo_battle_pc_aur_mb_en", "vo_battle_pc_aur_mc_en", 
            "vo_battle_pc_aur_ma_de", "vo_battle_pc_aur_mb_de", "vo_battle_pc_aur_mc_de",
            "vo_battle_pc_aur_ma_fr", "vo_battle_pc_aur_mb_fr", "vo_battle_pc_aur_mc_fr",
            "vo_battle_pc_aur_fa_ja", "vo_battle_pc_aur_fb_ja", "vo_battle_pc_aur_fc_ja",
            "vo_battle_pc_aur_fa_en", "vo_battle_pc_aur_fb_en", "vo_battle_pc_aur_fc_en", 
            "vo_battle_pc_aur_fa_de", "vo_battle_pc_aur_fb_de", "vo_battle_pc_aur_fc_de", 
            "vo_battle_pc_aur_fa_fr", "vo_battle_pc_aur_fb_fr", "vo_battle_pc_aur_fc_fr", 
            "vo_battle_pc_ros_ma_ja", "vo_battle_pc_ros_mb_ja", "vo_battle_pc_ros_mc_ja",
            "vo_battle_pc_ros_ma_en", "vo_battle_pc_ros_mb_en", "vo_battle_pc_ros_mc_en", 
            "vo_battle_pc_ros_ma_de", "vo_battle_pc_ros_mb_de", "vo_battle_pc_ros_mc_de", 
            "vo_battle_pc_ros_ma_fr", "vo_battle_pc_ros_mb_fr", "vo_battle_pc_ros_mc_fr",
            "vo_battle_pc_vie_ma_ja", "vo_battle_pc_vie_mb_ja", "vo_battle_pc_vie_mc_ja", 
            "vo_battle_pc_vie_ma_en", "vo_battle_pc_vie_mb_en", "vo_battle_pc_vie_mc_en",
            "vo_battle_pc_vie_ma_de", "vo_battle_pc_vie_mb_de", "vo_battle_pc_vie_mc_de",
            "vo_battle_pc_vie_ma_fr", "vo_battle_pc_vie_mb_fr", "vo_battle_pc_vie_mc_fr",
            "vo_battle_pc_vie_fa_ja", "vo_battle_pc_vie_fb_ja", "vo_battle_pc_vie_fc_ja",
            "vo_battle_pc_vie_fa_en", "vo_battle_pc_vie_fb_en", "vo_battle_pc_vie_fc_en",
            "vo_battle_pc_vie_fa_de", "vo_battle_pc_vie_fb_de", "vo_battle_pc_vie_fc_de", 
            "vo_battle_pc_vie_fa_fr", "vo_battle_pc_vie_fb_fr", "vo_battle_pc_vie_fc_fr" };
        public static List<string> GetValues() {
            List<string> paths = new List<string>();
            foreach (int value in emoteVoiceValues) {
                for (int i = 0; i < 14; i++) {
                    paths.Add(@"sound/voice/vo_emote/" + (value + i) + ".scd");
                }
            }
            foreach (string value in battleValues) {
                paths.Add(@"sound/voice/vo_battle/" + value + ".scd");
            }
            return paths;
        }
    }
}
