-- XanaduYonder: the existing FL8.0.5 game, restricted to its KR1 campaign.
-- Kept at the content boundary; entity templates and gameplay scripts stay original.
local P = { identity = "xanadu_kingdom_rush_1", locale = "zh-Hans" }
P.heroes = {"hero_gerald", "hero_alleria", "hero_bolin", "hero_magnus", "hero_ignus", "hero_malik", "hero_denas", "hero_ingvar", "hero_elora", "hero_oni", "hero_hacksaw", "hero_thor", "hero_10yr"}
P.hero_set = {}
for _, name in ipairs(P.heroes) do P.hero_set[name] = true end
P.tower_set = {}
for _, family in ipairs({"archer", "barrack", "mage", "engineer"}) do
    for tier = 1, 3 do P.tower_set["g1_tower_" .. family .. "_" .. tier] = true end
end
for _, name in ipairs({"tower_arcane_wizard", "tower_sorcerer", "tower_bfg", "tower_tesla", "tower_ranger", "tower_musketeer", "tower_paladin", "tower_barbarian", "tower_elf", "tower_sasquash", "tower_sunray"}) do P.tower_set[name] = true end

function P.settings(settings)
    settings = type(settings) == "table" and settings or {}
    settings.locale = P.locale
    settings.texture_size = "fullhd"
    settings.skip_settings_dialog = true
    if love and love.system and love.system.getOS() == "Android" then
        local host = require("jni_android")
        if host.get_system_property("SOUND_ENABLED") == "false" then settings.volume_fx = 0; settings.volume_music = 0 end
        if host.get_system_property("HAPTICS_ENABLED") == "false" then settings.volume_vibration = 0 end
    end
    return settings
end

function P.slot(slot)
    if type(slot) ~= "table" then return nil end
    local defaults = require("data.slot_template")
    slot.heroes = type(slot.heroes) == "table" and slot.heroes or {}
    slot.heroes.status = type(slot.heroes.status) == "table" and slot.heroes.status or {}
    if not P.hero_set[slot.heroes.selected] then slot.heroes.selected = "hero_gerald" end
    for name in pairs(slot.heroes.status) do
        if not P.hero_set[name] then slot.heroes.status[name] = nil end
    end
    for _, name in ipairs(P.heroes) do
        local status = slot.heroes.status[name]
        if type(status) ~= "table" then status = table.deepclone(defaults.heroes.status[name] or {xp=0, skills={}}); slot.heroes.status[name] = status end
        if type(status.xp) ~= "number" or status.xp < 0 then status.xp = 0 end
        if type(status.skills) ~= "table" then status.skills = {} end
    end
    slot.levels = type(slot.levels) == "table" and slot.levels or {}
    for index in pairs(slot.levels) do
        if type(index) ~= "number" or index < 45 or index > 76 or type(slot.levels[index]) ~= "table" then slot.levels[index] = nil end
    end
    slot.levels[45] = slot.levels[45] or {}
    if slot.last_victory and (type(slot.last_victory) ~= "table" or type(slot.last_victory.level_idx) ~= "number" or slot.last_victory.level_idx < 45 or slot.last_victory.level_idx > 76) then slot.last_victory = nil end
    slot.campaign_variant = "regular"
    slot.liuhui = type(slot.liuhui) == "table" and slot.liuhui or {}
    local lh = slot.liuhui
    -- Standalone native balance; these switches opt into FL's global template
    -- rewrites/UH hero behavior and are not required by the retained stages.
    lh.balance = false
    lh.g4range_balance = false
    lh.hero_enhance = false
    for _, name in ipairs({"cheat", "cheathero", "cheat5", "cheat6", "cheat5_special", "cheat5_dragon", "cp_mode", "fullscreen_tower_build"}) do lh[name] = false end
    for _, name in ipairs({"rand_creep", "rand_tower", "rand_tower_mode", "rand_hero", "reinforcement_skins"}) do lh[name] = 0 end
    lh.use3tower = true
    lh.use3tower_count = 1 -- Existing game_gui legacy-only branch.
    lh.tower_generations = {true, false, false, false, false}
    lh.power_selection = {slots={"fireball_g1", "reinforcement_g1", "empty_spell"}}
    local ordinary_defaults = {impossiblerate=1.5, hero_enhance=false, hero_auto_rally=false, g1_level10=false, g3_hprate=false, balance=false, enemy_count=1, g5_hero_dark_count=2, reinforcement_5="royal", g4range_balance=1}
    for key, value in pairs(ordinary_defaults) do if lh[key] == nil then lh[key] = value end end
    slot.xingyu = type(slot.xingyu) == "table" and slot.xingyu or {}
    if slot.xingyu.balance == nil then slot.xingyu.balance = 0 end
    slot.liuhui_hero = {usedoublehero=false, herolist={1,2}, rallylist={1,2,3,4}}
    slot.towers = {}
    slot.tower_pick = 0
    slot.reinforcement_count = 0
    slot.upgrades_by_generation = type(slot.upgrades_by_generation) == "table" and slot.upgrades_by_generation or {}
    for generation = 2, 3 do slot.upgrades_by_generation[generation] = {reinforcements=0,archers=0,thunder=0,rocks=0,barracks=0,mages=0} end
    slot.generation_upgrades = {}
    slot.seen = type(slot.seen) == "table" and slot.seen or {}
    slot.achievements = type(slot.achievements) == "table" and slot.achievements or {}
    slot.upgrades = type(slot.upgrades) == "table" and slot.upgrades or table.deepclone(defaults.upgrades)
    return slot
end

-- FL keeps its original global level IDs. Lua's array length is undefined for
-- this deliberately sparse table, so progression uses the explicit KR1 range.
function P.level_count(levels)
    local count = 0
    for index = 45, 76 do if levels[index] then count = count + 1 end end
    return count
end

function P.highest_level(levels)
    for index = 76, 45, -1 do if levels[index] then return index end end
    return 45
end

-- The original data includes a same-name KR2/KR1 pair. Preserve the selected
-- source row identity for display, while the unchanged reward API uses names.
local achievement_names
function P.achievement_allowed(name)
    if not achievement_names then
        achievement_names = {}
        for _, row in ipairs(require("firstgen_dependencies").achievement_rows) do achievement_names[row.name] = true end
    end
    return achievement_names[name] == true
end

function P.achievement_rows(data)
    local selected = {}
    for _, expected in ipairs(require("firstgen_dependencies").achievement_rows) do
        local row = data[expected.original_index]
        assert(row and row.name == expected.name and row.icon == expected.icon and row.goal == expected.goal, "firstgen: achievement source identity changed")
        selected[#selected+1] = row
    end
    return selected
end

function P.achievement_placeholder()
    return "achievement_icons_" .. string.format("%04i", require("firstgen_dependencies").achievement_rows[1].icon)
end

function P.animations(data)
    -- Later generation tables overwrite these five shared animation names.
    -- Restore the original native records after all merges, using immutable
    -- copied values rather than require-cache tables mutated by deepmerge.
    for name, original in pairs(require("firstgen_dependencies").native_animation_overrides) do
        data.animations[name] = table.deepclone(original)
    end
    return data
end

function P.hobboss_death_update(original_update)
    -- This zero-health, zero-speed controller exists only for the authored
    -- transformation death sequence. Health runs after main_script, so defer
    -- the original callback until it can enter its real death branch.
    return function(this, store, script)
        local sprite = this.render.sprites[1]
        sprite.name = nil
        sprite.animated = false
        while not this.health.dead do coroutine.yield() end
        sprite.animated = true
        return original_update(this, store, script)
    end
end

function P.map_data(data)
    local heroes, towers = {}, {}
    for _, row in ipairs(data.hero_data) do if P.hero_set[row.name] then heroes[#heroes+1] = row end end
    assert(#heroes == 13, "firstgen: expected all thirteen native heroes")
    data.hero_data = heroes
    for index, row in ipairs(data.tower_data) do
        if P.tower_set[row.name] and row.name ~= "tower_elf" and row.name ~= "tower_sasquash" and row.name ~= "tower_sunray" then
            row.icon = row.icon or index -- Preserve authored encyclopedia frame numbering.
            towers[#towers+1] = row
        end
    end
    data.tower_data = towers
    return data
end

function P.settings_data(settings)
    -- U.count_stars awards up to three campaign stars plus one heroic and one
    -- iron star for each original level in the selected 45..76 range.
    settings.max_stars = (76 - 45 + 1) * (3 + 1 + 1)
    local enemies = require("firstgen_dependencies").enemy_names
    if #enemies > 0 then
        local allowed, selected = {}, {}
        for _, name in ipairs(enemies) do allowed[name] = true end
        for index, row in ipairs(settings.encyclopedia_enemies) do
            if allowed[row.name] then
                row.original_index = row.original_index or index
                selected[#selected+1] = row
            end
        end
        settings.encyclopedia_enemies = selected
    end
    return settings
end

function P.game(game)
    assert(game.store.level_idx >= 45 and game.store.level_idx <= 76, "firstgen: level outside profile")
    assert(game.store.level_mode >= 1 and game.store.level_mode <= 3, "firstgen: unsupported mode")
    game.required_textures = {"go_decals", "go_enemies_common", "go_towers_1", "go_towers_2", "go_towers_special", "go_reinforcement_skin_0", "gui_common_123mod"}
    for _, name in ipairs(game.store.active_hero_names) do
        assert(P.hero_set[name], "firstgen: hero outside profile")
        game.required_textures[#game.required_textures+1] = "go_" .. name
    end
    for _, group in ipairs(require("firstgen_dependencies").game_texture_groups) do game.required_textures[#game.required_textures+1] = group end
    game.scale_required_textures = {}
    game.scale_required_textures_enemy = {}
    game.sc_branch_skill_textures = {}
    game.wave_editor_required_textures = {}
    game.wave_editor_scale_required_textures = {}
    game.store.dove43_required_exoskeletons = {}
    game.required_sounds = {"common", "firstgen_selected_sfx"}
end

function P.force_map(map)
    for generation = 1, 6 do map["kr" .. generation .. "_map"] = generation == 1 end
end

function P.log_missing(kind, name)
    local message = "[KR1_CONTENT_MISSING] " .. kind .. ": " .. tostring(name)
    print(message)
    if love and love.filesystem and love.filesystem.append then
        love.filesystem.append("content-missing.log", message .. "\n")
    end
end

return P
