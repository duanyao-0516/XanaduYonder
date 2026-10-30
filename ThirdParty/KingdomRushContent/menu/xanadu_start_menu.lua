-- Start-screen-only UI. No gameplay, storage, texture or audio logic is replaced.
local M = {}
local UI = {}
UI.__index = UI

local G = love.graphics
local FONT = "all-desktop/assets/fonts/HarmonyOS_Sans_SC.ttf"
local PAPER = {248, 245, 236, 255}
local INK = {41, 57, 45, 255}
local ACCENT = {70, 108, 76, 255}
local SECTIONS = {
    {"先看完整战场", "游戏画面大于手机屏幕，一定要在地图空白处上下拖动，查看入口、道路和终点；需要时也可左右拖动。不要把暂时看不到的区域误以为没有道路或敌人。"},
    {"目标与开始", "守住道路终点，击败每一波敌人。敌人到达终点会扣除生命，生命耗尽则失败。击败敌人可获得金币，用于建造和升级防御塔。首次游玩建议先选普通难度，逐步熟悉敌人与地形。"},
    {"建塔、升级与集结", "点击地图上的空建塔位，再选择防御塔；需要确认时再点一次。点击已有塔可以升级、购买高级技能或出售。兵营可以调整士兵集结点，把阻挡位置移到其他防御塔也能覆盖的道路上。建议尽快尝试每一种防御塔，再根据敌人搭配。"},
    {"四种基础防御塔", "箭塔：攻击快，适合对付低护甲敌人和空中敌人。兵营：派出士兵阻挡地面敌人，为其他塔争取输出时间。法师塔：魔法伤害擅长对付高物理护甲敌人，留意敌人的魔法抗性。炮兵塔：擅长地面范围伤害，适合密集敌群；高级塔还有各自的特殊能力。"},
    {"携带英雄与移动", "在英雄页面选择并携带英雄。英雄开局通常在道路终点附近，可以先拖动画面找到英雄，再移动到前线布防。点击英雄或可控制的人物，再点击地图上的目标位置即可移动。注意血量，及时后撤恢复，再与兵营和防御塔配合。"},
    {"骷髅图标与提前召唤", "每一波开始前，第一次点击骷髅图标会显示敌人的行进路径和来袭信息；再次点击可以提前召唤下一波。提前召唤会奖励金币，召唤越早，获得的额外金币越多。先观察上一波剩余敌人和英雄位置，再决定是否提前开波。"},
    {"援军、火雨与技能", "点击可用技能，再点击地图上的目标位置施放。援军适合临时阻挡漏怪，火雨适合处理密集或危险敌人。技能有冷却时间，不必一直留着不用，也不要把全部技能同时浪费在少量弱敌身上。"},
    {"星星、挑战与敌人", "完成战役可获得星星，用于升级防御塔与技能。英雄挑战、钢铁挑战使用同一张战场，但会改变波次、限制和胜利条件。查看图鉴了解敌人的护甲、魔法抗性、飞行或特殊能力，并及时调整防御组合。"},
    {"暂停、设置与存档", "战斗中可暂停查看局势。开始页面右上角的设置可调整本游戏音乐和音效，点击原设置面板的完成按钮保存。游戏会保存关卡进度；离开关卡前请留意确认提示，未完成的战斗不等于已经通关。"}
}

local function rect(x, y, w, h)
    return {x = x, y = y, w = w, h = h}
end

local function contains(r, x, y)
    return r and x >= r.x and y >= r.y and x < r.x + r.w and y < r.y + r.h
end

local function clamp(value, minimum, maximum)
    return math.max(minimum, math.min(maximum, value))
end

function UI:release_fonts()
    if self.fonts then
        for _, font in pairs(self.fonts) do
            font:release()
        end
        self.fonts = nil
    end
end

function UI:refresh_layout()
    local w, h = G.getDimensions()
    if self.layout and self.width == w and self.height == h then
        return
    end
    self.width, self.height = w, h
    local s = math.max(0.85, math.min(w / 1280, h / 720))
    self.scale = s
    self:release_fonts()
    self.fonts = {
        body = G.newFont(FONT, math.floor(18 * s)),
        heading = G.newFont(FONT, math.floor(21 * s)),
        small = G.newFont(FONT, math.floor(16 * s))
    }
    local margin, bh, gap = 18 * s, 44 * s, 10 * s
    local settings_width, help_width = 104 * s, 140 * s
    local pw = math.min(w - margin * 2, 780 * s)
    local ph = math.min(h - margin * 2, 600 * s)
    local px, py = (w - pw) / 2, (h - ph) / 2
    self.layout = {
        settings = rect(w - margin - settings_width, margin, settings_width, bh),
        help = rect(w - margin - settings_width - gap - help_width, margin, help_width, bh),
        panel = rect(px, py, pw, ph),
        close = rect(px + pw - 54 * s, py + 10 * s, 40 * s, 40 * s),
        done = rect(px + pw - 110 * s, py + ph - 52 * s, 90 * s, 36 * s),
        content = rect(px + 24 * s, py + 64 * s, pw - 55 * s, ph - 126 * s)
    }
    local y, rows = 0, {}
    for _, section in ipairs(SECTIONS) do
        local _, heading_lines = self.fonts.heading:getWrap(section[1], self.layout.content.w)
        local _, body_lines = self.fonts.body:getWrap(section[2], self.layout.content.w)
        local heading_height = #heading_lines * self.fonts.heading:getHeight()
        local body_height = #body_lines * self.fonts.body:getHeight()
        rows[#rows + 1] = {heading = section[1], body = section[2], y = y, body_y = y + heading_height + 8 * s}
        y = y + heading_height + body_height + 30 * s
    end
    self.rows = rows
    self.max_scroll = math.max(0, y - self.layout.content.h)
    self.scroll = clamp(self.scroll, 0, self.max_scroll)
    -- A resize can occur between press and release. Keep consuming that gesture
    -- instead of releasing it onto the original start screen at new coordinates.
    if self.capture then
        self.capture = {kind = "modal"}
    end
end

function UI:controls_available()
    local window = self.screen.window
    if not window or window._disabled then
        return false
    end
    for _, id in ipairs({"options_view", "quit_view", "delete_view"}) do
        local view = window:get_child_by_id(id)
        if view and not view.hidden then
            return false
        end
    end
    return true
end

local function rounded(r, color, radius)
    G.setColor(color)
    G.rectangle("fill", r.x, r.y, r.w, r.h, radius, radius)
end

local function book(x, y, size)
    G.rectangle("line", x, y + size * 0.1, size, size * 0.8, size * 0.07)
    G.line(x + size / 2, y + size * 0.1, x + size / 2, y + size * 0.9)
    for i = 1, 3 do
        local yy = y + size * (0.2 + i * 0.14)
        G.line(x + size * 0.13, yy, x + size * 0.37, yy)
        G.line(x + size * 0.63, yy, x + size * 0.87, yy)
    end
end

local function gear(x, y, size)
    local cx, cy = x + size / 2, y + size / 2
    G.circle("line", cx, cy, size * 0.30)
    G.circle("line", cx, cy, size * 0.11)
    for i = 0, 7 do
        local a = i * math.pi / 4
        G.line(cx + math.cos(a) * size * 0.32, cy + math.sin(a) * size * 0.32,
            cx + math.cos(a) * size * 0.46, cy + math.sin(a) * size * 0.46)
    end
end

function UI:draw_button(r, text, icon)
    rounded(r, {231, 237, 228, 255}, 8 * self.scale)
    G.setColor(INK)
    G.setLineWidth(2 * self.scale)
    icon(r.x + 12 * self.scale, r.y + 11 * self.scale, 22 * self.scale)
    G.setFont(self.fonts.body)
    G.print(text, r.x + 43 * self.scale, r.y + (r.h - self.fonts.body:getHeight()) / 2)
end

function UI:draw()
    self:refresh_layout()
    if not self.open and not self:controls_available() then
        return
    end
    G.push("all")
    G.origin()
    G.setScissor()
    self:draw_button(self.layout.help, "玩法说明", book)
    self:draw_button(self.layout.settings, "设置", gear)
    if self.open then
        local p, c, s = self.layout.panel, self.layout.content, self.scale
        G.setColor(0, 0, 0, 158)
        G.rectangle("fill", 0, 0, self.width, self.height)
        rounded(p, PAPER, 12 * s)
        G.setColor(ACCENT)
        G.setLineWidth(2 * s)
        book(p.x + 24 * s, p.y + 20 * s, 25 * s)
        G.setFont(self.fonts.heading)
        G.print("玩法说明", p.x + 62 * s, p.y + 18 * s)
        local close = self.layout.close
        G.line(close.x + 13 * s, close.y + 13 * s, close.x + 27 * s, close.y + 27 * s)
        G.line(close.x + 27 * s, close.y + 13 * s, close.x + 13 * s, close.y + 27 * s)
        G.setScissor(c.x, c.y, c.w, c.h)
        for _, row in ipairs(self.rows) do
            G.setColor(ACCENT)
            G.setFont(self.fonts.heading)
            G.printf(row.heading, c.x, c.y + row.y - self.scroll, c.w, "left")
            G.setColor(INK)
            G.setFont(self.fonts.body)
            G.printf(row.body, c.x, c.y + row.body_y - self.scroll, c.w, "left")
        end
        G.setScissor()
        if self.max_scroll > 0 then
            local thumb_h = math.max(24 * s, c.h * c.h / (self.max_scroll + c.h))
            local thumb_y = c.y + self.scroll / self.max_scroll * (c.h - thumb_h)
            rounded(rect(c.x + c.w + 9 * s, c.y, 4 * s, c.h), {220, 222, 212, 255}, 2 * s)
            rounded(rect(c.x + c.w + 9 * s, thumb_y, 4 * s, thumb_h), ACCENT, 2 * s)
        end
        G.setFont(self.fonts.small)
        G.setColor(INK)
        G.print("上下滑动查看", p.x + 24 * s, p.y + p.h - 42 * s)
        rounded(self.layout.done, {231, 237, 228, 255}, 7 * s)
        G.setColor(INK)
        G.setFont(self.fonts.body)
        G.printf("知道了", self.layout.done.x, self.layout.done.y + 7 * s, self.layout.done.w, "center")
    end
    G.pop()
end

function UI:mousepressed(x, y, button)
    self:refresh_layout()
    if self.open then
        local kind = "modal"
        if button == 1 then
            if contains(self.layout.close, x, y) or contains(self.layout.done, x, y) then
                kind = "close"
            elseif contains(self.layout.content, x, y) then
                kind = "scroll"
            end
        end
        self.capture = {kind = kind, start_y = y, start_scroll = self.scroll}
        return true
    end
    if button == 1 and self:controls_available() then
        for _, kind in ipairs({"help", "settings"}) do
            if contains(self.layout[kind], x, y) then
                self.capture = {kind = kind}
                return true
            end
        end
    end
    return false
end

function UI:update()
    self:refresh_layout()
    local capture = self.capture
    if capture and capture.kind == "scroll" and love.mouse.isDown(1) then
        local _, y = love.mouse.getPosition()
        self.scroll = clamp(capture.start_scroll + capture.start_y - y, 0, self.max_scroll)
    end
end

function UI:mousereleased(x, y, button)
    local capture = self.capture
    self.capture = nil
    if not capture then
        return self.open
    end
    if button == 1 then
        if capture.kind == "help" and contains(self.layout.help, x, y) then
            self.open, self.scroll = true, 0
        elseif capture.kind == "settings" and contains(self.layout.settings, x, y) then
            self.screen.window:get_child_by_id("options_view"):show()
        elseif capture.kind == "close" and (contains(self.layout.close, x, y) or contains(self.layout.done, x, y)) then
            self.open = false
        elseif capture.kind == "scroll" then
            self.scroll = clamp(capture.start_scroll + capture.start_y - y, 0, self.max_scroll)
        end
    end
    -- Consume the release even after closing, so it cannot reach a save slot.
    return true
end

function UI:keypressed(key)
    if not self.open then
        return false
    end
    if key == "escape" then
        self.open = false
        if self.capture then
            self.capture = {kind = "modal"}
        end
    end
    return true
end

function UI:wheelmoved(_, dy)
    if not self.open then
        return false
    end
    self.scroll = clamp(self.scroll - dy * 48 * self.scale, 0, self.max_scroll)
    return true
end

function M.attach(screen)
    if screen._xanadu_start_menu_attached then
        return
    end
    screen._xanadu_start_menu_attached = true
    local init, destroy, draw, update = screen.init, screen.destroy, screen.draw, screen.update
    function screen:init(...)
        init(self, ...)
        self.xanadu_start_menu = setmetatable({screen = self, open = false, scroll = 0}, UI)
        self.xanadu_start_menu:refresh_layout()
    end
    function screen:destroy(...)
        if self.xanadu_start_menu then
            self.xanadu_start_menu:release_fonts()
            self.xanadu_start_menu = nil
        end
        return destroy(self, ...)
    end
    function screen:draw(...)
        draw(self, ...)
        if self.xanadu_start_menu then
            self.xanadu_start_menu:draw()
        end
    end
    function screen:update(...)
        update(self, ...)
        -- The original timer can navigate and destroy this screen in update.
        if self.xanadu_start_menu then
            self.xanadu_start_menu:update()
        end
    end
    for _, method in ipairs({"mousepressed", "mousereleased", "keypressed", "wheelmoved"}) do
        local original = screen[method]
        screen[method] = function(self, ...)
            if self.xanadu_start_menu and self.xanadu_start_menu[method](self.xanadu_start_menu, ...) then
                return
            end
            if original then
                return original(self, ...)
            end
        end
    end
end

return M
