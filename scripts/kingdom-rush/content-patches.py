"""Strict source adaptations for the supplied FL 8.0.5 Android content tree.
Waves, combat balance, atlas geometry and immutable original files are unchanged.
Achievement progress ranges follow the selected sparse campaign IDs.
"""
from pathlib import Path
import re


def patch_tree(root: Path, profile: Path):
    changed = {}
    def edit(path, function):
        target = root / path
        old = target.read_text(encoding='utf-8-sig')
        new = function(old)
        if new != old:
            target.write_text(new, encoding='utf8')
            record = changed.setdefault(path, {'original_characters':len(old)})
            record['patched_characters'] = len(new)
    def one(text, old, new):
        if text.count(old) != 1: raise ValueError(f'Expected one source anchor, got {text.count(old)}: {old[:100]}')
        return text.replace(old,new,1)
    def between(text, start, end, new):
        a=text.index(start); b=text.index(end,a)
        return text[:a]+new+text[b:]
    (root/'firstgen.lua').write_bytes(profile.read_bytes())
    edit('conf.lua', lambda s: one(s, 't.modules.physics = false', 't.identity = "xanadu_kingdom_rush_1"\n\tt.window = t.window or {}\n\tt.window.title = "王国保卫战"\n\tt.modules.physics = false'))
    edit('version.lua', lambda s: re.sub(r'identity\s*=\s*"[^"]+"', 'identity = "xanadu_kingdom_rush_1"',s))
    edit('main.lua', lambda s: one(one(s, 'function main:set_locale(locale)', 'function main:set_locale(locale)\n\tlocale = "zh-Hans"'), 'MU.apply_params(main.params, KR_GAME, KR_TARGET, KR_PLATFORM)', 'require("firstgen").settings(main.params)\n\tMU.apply_params(main.params, KR_GAME, KR_TARGET, KR_PLATFORM)'))
    edit('kr3/game_settings.lua', lambda s: one(one(s, 'GS.default_hero = "hero_elves_archer"', 'GS.default_hero = "hero_gerald"'), 'return GS', 'return require("firstgen").settings_data(GS)'))
    edit('kr3/data/slot_template.lua', lambda s: one(s, 'selected = "hero_elves_archer"', 'selected = "hero_gerald"'))
    def storage(s):
        s=one(s,'function storage:save_slot(data_table, idx, should_sync)','function storage:save_slot(data_table, idx, should_sync)\n\tdata_table = require("firstgen").slot(data_table)')
        start=s.index('function storage:load_slot('); end=s.index('function storage:save_slot(',start)
        part=s[start:end].replace('return input','return require("firstgen").slot(input)')
        s=s[:start]+part+s[end:]
        s=between(s,'function storage:load_settings()','function storage:save_settings(','''function storage:load_settings()
    return require("firstgen").settings(self:load_lua(self.SETTINGS_FILE))
end

''')
        s=s.replace('not type(input) == "table"','type(input) ~= "table"')
        s=one(s, 'for i = 1, GS.last_level do', 'for i = 45, 76 do')
        return s
    edit('all/storage.lua',storage)
    def achievement_handlers(s):
        s=one(s,'for i = 1, GS.last_level do','for i = 45, 76 do')
        s=one(s,'for i = 1, GS.main_campaign_levels do','for i = 45, 56 do')
        return one(s,'done_c, done_h, done_i, done_g = false, false, false, false','done_c, done_h, done_i, done_g, done_imp = false, false, false, false, false')
    edit('kr3/achievements_handlers.lua',achievement_handlers)
    edit('kr3/game_templates-1.lua', lambda s: one(s, 'tt = RT("decal_gold_bag_iron_count", "decal_tween")\n\nAC(tt, "texts")\n\ntt.render.sprites[1].hidden = true', 'tt = RT("decal_gold_bag_iron_count", "decal_tween")\n\nAC(tt, "texts")\n\n-- Hidden tween anchor has no image; sprite 2 receives the real generated text.\ntt.render.sprites[1].name = nil\ntt.render.sprites[1].hidden = true'))
    def effect_only_projectiles(s):
        s=one(s, 'tt.render.sprites[1].name = "time_wizard_sandstorm_proj"', '-- Original source has no projectile image; retain its real sandstorm hit effect and aura.\ntt.render.sprites[1].name = nil')
        return one(s, 'tt.render.sprites[1].name = "pirateTower_bombasticwhat"', '-- Original source has no projectile image; retain the real airstrike explosion and damage delay.\ntt.render.sprites[1].name = nil')
    edit('kr3/game_templates-1.lua',effect_only_projectiles)
    def effect_only_controllers(s):
        def section(text, start, end, transform):
            a=text.index(start); b=text.index(end,a)
            return text[:a]+transform(text[a:b])+text[b:]
        s=section(s, 'tt = RT("enemy_hobboss_swap", "enemy")', 'tt = RT("enemy_tp_aura", "enemy")', lambda part: one(part, 'tt.main_script.update = scripts.enemy_passive.update', 'tt.main_script.update = require("firstgen").hobboss_death_update(scripts.enemy_passive.update)'))
        for name,end in [('enemy_tp_aura','tt = RT("enemy_curse_aura", "enemy")'),('enemy_curse_aura','tt = E.register_t(E, "missile_hobgob", "bullet")')]:
            s=section(s, 'tt = RT("'+name+'", "enemy")', end, lambda part: one(part, 'tt.render.sprites[1].name = "start"', '-- The separate child aura owns the real visuals; this hp-zero controller has no image.\ntt.render.sprites[1].name = nil\ntt.render.sprites[1].animated = false\ntt.unit.death_animation = nil'))
        s=section(s, 'tt = RT("hobgoblin_bomb_spawner", "decal_scripted")', 'tt = RT("hobgoblin_meteor_spawner", "decal_scripted")', lambda part: one(part, 'tt.render.sprites[1].prefix = "bomb_hobgoblin_spawner"', 'tt.render.sprites[1].prefix = "bomb_hobgoblin_spawner"\n-- No authored image exists for this controller; retain spawning and tween timing.\ntt.render.sprites[1].name = nil\ntt.render.sprites[1].animated = false\ntt.spawner.initial_spawn_animation = nil'))
        return s
    edit('kr3/game_templates-1.lua',effect_only_controllers)
    edit('kr3/data/game_animations.lua', lambda s: one(s, 'return d1', 'return require("firstgen").animations(d1)'))
    edit('kr3/upgrades.lua', lambda s: one(s, 'if user_data.liuhui and user_data.liuhui.g4range_balance ~= nil and user_data.liuhui.g4range_balance == false then\n\t\tupgrades_FL:range_g4()\n\tend', '-- firstgen: the foreign KR4 range rewrite is outside this profile'))
    edit('all/achievements.lua', lambda s: one(s, 'function A:got(id)', 'function A:got(id)\n\tif not require("firstgen").achievement_allowed(id) then return end'))
    edit('all-desktop/game_gui.lua', lambda s: one(s, 'KImageView:new("achievement_icons_0001")', 'KImageView:new(require("firstgen").achievement_placeholder())'))
    def chinese_fonts(s):
        s=one(s, 'local name = self.font_subst[alias] or alias', 'local name = self.font_subst[alias] or alias\n\tif name == "msyh" or name == "NotoSansCJKjp-Regular" or name == "NotoSansCJKkr-Regular" then name = "HarmonyOS_Sans_SC" end')
        return one(s, 'self.fonts[name_size] = font', 'self.fonts[name_size] = font\n\t\t\tif name ~= "HarmonyOS_Sans_SC" and font.setFallbacks then\n\t\t\t\tlocal fallback = self:f("HarmonyOS_Sans_SC", real_size)\n\t\t\t\tfont:setFallbacks(fallback)\n\t\t\tend')
    edit('lib/klove/font_db.lua',chinese_fonts)
    edit('main.lua',lambda s: one(s,'all-desktop/assets/fonts/msyh.ttf','all-desktop/assets/fonts/HarmonyOS_Sans_SC.ttf'))
    edit('all/i18n.lua',lambda s: between(s.replace('i18n.default_locale = "en"','i18n.default_locale = "zh-Hans"').replace('i18n.current_locale = "en"','i18n.current_locale = "zh-Hans"'), 'i18n.supported_locales = {', 'i18n.locale_names =', 'i18n.supported_locales = {"zh-Hans"}\n'))
    (root/'jni_android.lua').write_text('return require("libjni_android")\n')
    changed['jni_android.lua'] = {'reason':'Real Android JNI module; missing native module is a hard error'}
    edit('kr3-desktop/data/map_data.lua', lambda s: one(s,'return map_data','return require("firstgen").map_data(map_data)'))
    edit('kr3-desktop/data/director_data.lua', lambda s: one(s,'return d','d.loading_image_name.default = "loading_grass"\n\nreturn d'))
    def native_extension_ending(s):
        # Both supplied APK and PC archives lack the FA-specific credits and
        # music definitions. Use the real native credits/music already shipped.
        s=one(s,'require("data.credits_data_fa")','require("data.credits_data")')
        s=one(s,'"music_screen_kr1_end_fa"','"music_screen_kr1_end"')
        s=one(s,'"MusicEndCreditsFA"','"MusicEndCredits1"')
        return one(s,'level_idx = 33','level_idx = 75')
    edit('kr3/screen_kr1_fa.lua',native_extension_ending)
    def director(s):
        s=one(s, 'game.store.user_data = user_data', 'game.store.user_data = user_data\n\t\trequire("firstgen").game(game)')
        s=between(s,'\t\tlocal wave_editor_ok, wave_editor_store =', '\t\t-- collectgarbage()', '')
        # Generic texture/sound loading stays strict. Only optional global groups are replaced.
        s=one(s,'game.store.campaign_variant = args.campaign_variant or CAMPAIGN_VARIANT_REGULAR', 'assert(args.level_idx >= 45 and args.level_idx <= 76, "firstgen level outside profile")\n\t\targs.campaign_variant = CAMPAIGN_VARIANT_REGULAR\n\t\tgame.store.campaign_variant = CAMPAIGN_VARIANT_REGULAR')
        return s
    edit('all/director.lua',director)
    def systems(s):
        for name in ['hero_dianyun','hero_mammoth_exoskeletons','enemy_table_stage3','enemy_table_stage4','enemy_table_stage5']:
            s=one(s,'EXO:load('+name+')','-- firstgen: optional unrelated global batch removed: '+name)
        # Selected extension stages may still declare skeletons even below legacy threshold 87.
        s=one(s, 'EXO:load(store.dove43_required_exoskeletons)', 'EXO:load(require("firstgen_dependencies").exoskeletons)')
        s=one(s,'if store.level_idx >= 87 then','if store.level_idx >= 45 and store.level_idx <= 76 then')
        return s
    edit('all/systems.lua',systems)
    def map_screen(s):
        s=one(s,'achievements_data = require("data.achievements_data")','achievements_data = require("firstgen").achievement_rows(require("data.achievements_data"))')
        s=one(s,'KImageView:new("achievement_icons_0001")','KImageView:new(require("firstgen").achievement_placeholder())')
        s=one(s,'function screen_map:init(w, h, done_callback)','function screen_map:init(w, h, done_callback)\n\trequire("firstgen").force_map(self)')
        a=s.index('\telseif screen_map.kr1_map then\n\t\tKImageView.initialize(self, "map_background1")'); b=s.index('\telseif screen_map.kr2_map then',a)
        part=one(s[a:b], 'local last_flag_idx = screen_map.unlock_data.new_level or #screen_map.user_data.levels', 'local last_flag_idx = (screen_map.unlock_data.new_level or require("firstgen").highest_level(screen_map.user_data.levels)) - 44')
        s=s[:a]+part+s[b:]
        s=s.replace('#self.user_data.levels', 'require("firstgen").level_count(self.user_data.levels)').replace('#screen_map.user_data.levels', 'require("firstgen").level_count(screen_map.user_data.levels)')
        s=one(s, 'self.unlock_data.unlocked_levels = {\n\t\t\t1\n\t\t}\n\t\tlevels[1] = {}', 'self.unlock_data.unlocked_levels = {45}\n\t\tlevels[45] = {}')
        s=one(s, 'self.hero_icon_portrait = KImageView:new("mapButtons_portrait_hero_0001")', 'self.hero_icon_portrait = KImageView:new("mapButtons_portrait_hero_0033")')
        if 'KImageView:new("portrait_notxt_0001")' not in s:raise ValueError('Missing original hero portrait initialization')
        s=s.replace('KImageView:new("portrait_notxt_0001")','KImageView:new("portrait_notxt_0033")')
        s=one(s, 'local icon_name = menu_icon or mapped_menu_icon or power_icon or encyclopedia_icon or menu_item and menu_item.image', 'local icon_name = menu_icon or power_icon or encyclopedia_icon or mapped_menu_icon or menu_item and menu_item.image')
        # These controls rewrite native/foreign template balance, rather than
        # selecting any required campaign facility. Remove their constructions.
        s=between(s,'local function tower_select_add_quick_balance_buttons(', 'local function encyclopedia_add_tower_power_icons(', '')
        if s.count('\ttower_select_add_quick_balance_buttons(self, index)') != 2:raise ValueError('Expected encyclopedia and tower-select balance call sites')
        s=s.replace('\ttower_select_add_quick_balance_buttons(self, index)', '')
        s=one(s, 'title_label.text = show_balance_status == false and title or string.format("%s（%s）", title or "", encyclopedia_tower_doc_status())', 'title_label.text = title or ""')
        s=one(s, 'encyclopedia_enemy_icon_idx(t, creeps_data[i].name, i, creeps_data[i].generation)', 'encyclopedia_enemy_icon_idx(t, creeps_data[i].name, creeps_data[i].original_index, creeps_data[i].generation)')
        s=one(s, 'encyclopedia_enemy_icon_idx(t, creep_data.name, index, creep_data.generation)', 'encyclopedia_enemy_icon_idx(t, creep_data.name, creep_data.original_index, creep_data.generation)')
        s=between(s,'screen_map.required_sounds = {','screen_map.ref_w =', '''screen_map.required_sounds = {"common", "music_screen_map", "firstgen_selected_sfx"}
screen_map.required_textures = {"screen_map_123", "screen_map_hud", "legacy_generation_upgrades", "generation_upgrades", "room_hero", "view_options", "achievements", "encyclopedia", "encyclopedia_thumbs", "encyclopedia_towers", "encyclopedia_creeps"}
for _, group in ipairs(require("firstgen_dependencies").map_texture_groups) do table.insert(screen_map.required_textures, group) end

''')
        # Remove entire optional constructions; do not instantiate missing foreign resources then hide them.
        s=between(s,'\t--地图切换按钮','\t------多种选项','')
        s=between(s,'\t------多种选项','\tself.window:add_child(more_button)', '')
        s=one(s,'\tself.window:add_child(more_button)\t','')
        for field, next_marker in [('spell_select','\t--新增防御塔选择界面tower_room'),('tower_room','\t--新增5代英雄选择界面hero5_room'),('hero5_room','\t-------多种选项'),('more_option_panel','\t-------\n\tself.difficulty_view')]:
            start='\tself.'+field+' = configure_android_map_popup('
            s=between(s,start,next_marker,'')
        s=between(s,'\tself.developer_mode_panel = configure_android_map_popup(', '\tif IS_ANDROID then','')
        s=between(s,'\tlocal wave_editor_sw =', '\tif self.kr1_map then\n\t\tS:queue("MusicMap1")','')
        s=s.replace('elseif not self.more_option_panel.hidden then','elseif self.more_option_panel and not self.more_option_panel.hidden then')
        s=s.replace('if not self.more_option_panel.hidden then','if self.more_option_panel and not self.more_option_panel.hidden then')
        s=s.replace('self.more_option_panel:show()','if self.more_option_panel then self.more_option_panel:show() end')
        for field in ['spell_select', 'tower_room']:
            s=s.replace('if not self.'+field+'.hidden then', 'if self.'+field+' and not self.'+field+'.hidden then')
            s=s.replace('self.'+field+':show()', 'if self.'+field+' then self.'+field+':show() end')
        for field in ['tower5_room', 'spell_select_button', 'more_button'] + ['change_button'+str(i) for i in range(1,7)]:
            for action in ['add_child', 'remove_child']:
                call='self.window:'+action+'(self.'+field+')'
                s=s.replace(call,'if self.'+field+' then '+call+' end')
            s=s.replace('self.'+field+':on_click()', 'if self.'+field+' then self.'+field+':on_click() end')
        s=s.replace('for _, popup in ipairs(popups) do','for _, popup in pairs(popups) do')
        # One regular campaign variant, same original heroic and iron modes.
        s=between(s,'local level_select_campaign_variants = {','local level_select_campaign_variant_labels =', 'local level_select_campaign_variants = {CAMPAIGN_VARIANT_REGULAR}\n')
        s=one(s,'self.campaign_variant_button = variant_button','variant_button.hidden = true\n\tvariant_button.disabled = true\n\tself.campaign_variant_button = variant_button')
        # Existing first-generation technology tree; foreign pages do not construct assets.
        a=s.index('function UpgradesView:initialize('); b=s.index('function UpgradesView:hide_shared_tech_tree_help',a)
        part=s[a:b].replace('for generation = 1, 3 do','for generation = 1, 1 do').replace('\tbuild_generation_page(4)\n\tbuild_generation_page(5)','').replace('for page_index = 1, 5 do','for page_index = 1, 1 do')
        s=s[:a]+part+s[b:]
        s=s.replace('self.current_page = km.clamp(1, 5, page)','self.current_page = 1').replace('self.legacy_pages[generation].hidden = self.current_page ~= generation','if self.legacy_pages[generation] then self.legacy_pages[generation].hidden = self.current_page ~= generation end')
        # Existing hero thumbnails use the filtered hero_data roster. No foreign pages.
        s=one(s, 'self:create_hero_deck_panel()', '-- firstgen: single original hero selection')
        a=s.index('function HeroRoomView:initialize('); b=s.index('function HeroRoomView:update_hero_enhance_button',a)
        part=s[a:b].replace('self.switch_button = switch_button', 'switch_button.hidden = true\n\tswitch_button.disabled = true\n\tself.switch_button = switch_button')
        part=between(part,'\tlocal hero_enhance_button = GGButton:new(', '\tlocal hero_auto_rally_button = GGButton:new(', '')
        part=one(part,'v(hero_enhance_button.pos.x - done_button.size.x - 20, self.g1_level_but.pos.y)', 'v(next_page_button.pos.x - done_button.size.x - 20, self.g1_level_but.pos.y)')
        s=s[:a]+part+s[b:]
        # Settings developer entry would access removed editor/cheat panel.
        s=s.replace('screen_map.developer_mode_panel:show()', 'return')
        s=one(s, 'screen_map.new_player_setup_stage = "tower"', 'screen_map.new_player_setup_stage = nil')
        s=s.replace('screen_map.tower_room:show()', 'if screen_map.tower_room then screen_map.tower_room:show() end')
        s=one(s, 'self.back:add_child(developer_mode_button)', 'developer_mode_button.hidden = true\n\tdeveloper_mode_button.disabled = true\n\tself.back:add_child(developer_mode_button)')
        for field in ['spell_select', 'tower_room']:
            s=s.replace('elseif not self.'+field+'.hidden then', 'elseif self.'+field+' and not self.'+field+'.hidden then')
        a=s.index('function UpgradesView:set_stars_and_check()'); b=s.index('function UpgradesView:rest_stars',a)
        part=s[a:b].replace('for generation = 1, 3 do','for generation = 1, 1 do').replace('for current_generation = 1, 3 do','for current_generation = 1, 1 do')
        s=s[:a]+part+s[b:]
        s=one(s, 'self.kr6_tree = Kr6HeroUpgradeTreeView:new(self)\n\tself.kr6_tree.pos = v((self.back.size.x - self.kr6_tree.size.x) / 2, 220)\n\tself.back:add_child(self.kr6_tree)', '-- firstgen: foreign hero technology view excluded')
        s=one(s, 'b = GGOptionsButton:new(_("BUTTON_QUIT"))', 'b = GGOptionsButton:new("返回大厅")')
        s=one(s, 'screen_map.done_callback({\n\t\t\tnext_item_name = "slots"\n\t\t})', 'storage:save_slot(screen_map.user_data, nil, true)\n\t\tlove.event.quit()')
        return s
    edit('all-desktop/screen_map.lua',map_screen)
    def sound(s):
        anchor='\tif not self.groups[name] then\n\t\tlog.error("sound group %s not found", name)'
        replacement='\tif not self.groups[name] and self.sounds[name] then\n\t\tself.groups[name] = {sounds={name}}\n\tend\n'+anchor.replace('log.error("sound group %s not found", name)','require("firstgen").log_missing("sound-group", name)\n\t\tlog.error("sound group %s not found", name)')
        s=one(s,anchor,replacement)
        return one(s, 'local function sound_db_queue_group_files(self, name)\n\tlocal group = self.groups[name]', 'local function sound_db_queue_group_files(self, name)\n\tif not self.groups[name] and self.sounds[name] then self.groups[name] = {sounds={name}} end\n\tlocal group = self.groups[name]')
    edit('all/sound_db.lua',sound)
    def image_diagnostics(s):
        for marker,kind,value in [
            ('log.error("atlas file %s not found for %s/%s", group_file, path, name)','atlas-file','group_file'),
            ('log.error("Image %s not found in the images db\\n%s", name, self:get_short_stats())','image','name'),
            ('log.error("Sprite %s was not found in the atlas db.\\n%s", name, self:get_short_stats())','sprite','name')]:
            s=one(s,marker,'require("firstgen").log_missing("'+kind+'", '+value+')\n\t\t\t'+marker)
        return s
    edit('lib/klove/image_db.lua',image_diagnostics)
    def gui_atlas(s):
        s=one(s, 'local z2 = require("assets.images.fullhd." .. "gui_common_4")\n', '')
        return one(s, 'for k, v in pairs(z2) do\n\tif z[k] and z[k] ~=v then\n\t\tcount=count+1\n\tend\n\tz[k] = v\nend\n', '')
    edit('kr3-desktop/assets/images/fullhd/gui_common.lua',gui_atlas)
    edit('main.lua', lambda s: one(s, 'function love.quit()\n', 'function love.quit()\n\tif main.params then storage:save_settings(main.params, true) end\n'))
    edit('all-desktop/game_gui.lua', lambda s: between(s, 'game_gui.required_textures = {', 'game_gui.ref_h =', 'game_gui.required_textures = {"gui_common", "gui_common_123", "gui_common_123mod", "spell_selection_icons", "gui_portraits", "gui_portraits_123", "gui_portraits_20260307", "achievements", "encyclopedia", "gui_notifications_common", "gui_notifications_common_123", "gui_notifications_bg", "gui_notifications_bg_123", "view_options", "white_block", "gui_slices", "gui_tutorial"}\ngame_gui.ref_h =') .replace('game_gui.ref_h =game_gui.ref_h =', 'game_gui.ref_h ='))
    unused = root/'kr3-desktop/data/metagame_settings.lua'
    if unused.exists():
        unused.unlink()
        changed['kr3-desktop/data/metagame_settings.lua'] = {'removed':'unreferenced KR4 plist export has invalid raw newlines in quoted Lua strings'}
    return changed
