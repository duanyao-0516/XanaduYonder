#!/usr/bin/env python3
"""Exercise actual menu Lua with recording graphics and existing-screen adapters."""
import argparse
from pathlib import Path
import sys


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--pythonlib', type=Path, required=True)
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    source = root / 'ThirdParty/KingdomRushContent/menu/xanadu_start_menu.lua'
    assert source.is_file(), 'The actual start-menu UI has not been implemented'
    sys.path.insert(0, str(args.pythonlib))
    from lupa.lua51 import LuaRuntime
    lua = LuaRuntime(unpack_returned_tuples=True)
    lua.execute('''
        counters = {init=0, destroy=0, draw=0, update=0, down=0, up=0, key=0, settings=0}
        viewport_w, viewport_h = 2400, 1080
        mouse_x, mouse_y, mouse_down = 0, 0, false
        local function noop() end
        local function font(size)
          return {getHeight=function() return size end,
            getWrap=function(_,text,width)
              local lines={}
              local chars=math.max(1, math.floor(width / size))
              for i=1,math.max(1,math.ceil(#text / (chars*2))) do lines[#lines+1]='line' end
              return width,lines
            end,
            release=noop}
        end
        love={graphics={getDimensions=function() return viewport_w,viewport_h end,
          newFont=function(_,size) return font(size) end,
          push=noop,pop=noop,origin=noop,setScissor=noop,setColor=noop,
          setFont=noop,rectangle=noop,line=noop,circle=noop,polygon=noop,
          printf=noop,print=noop,setLineWidth=noop,setLineJoin=noop},
          mouse={getPosition=function() return mouse_x,mouse_y end,
            isDown=function() return mouse_down end}}
        options={hidden=true,show=function(self) self.hidden=false; counters.settings=counters.settings+1 end}
        hidden={hidden=true}
        window={get_child_by_id=function(_,id) return id=='options_view' and options or hidden end}
        screen={init=function(self) counters.init=counters.init+1; self.window=window end,
          destroy=function(self) counters.destroy=counters.destroy+1; self.window=nil end,
          draw=function(self) counters.draw=counters.draw+1; if destroy_during_draw then self:destroy() end end,
          update=function(self) counters.update=counters.update+1; if destroy_during_update then self:destroy() end end,
          mousepressed=function() counters.down=counters.down+1 end,
          mousereleased=function() counters.up=counters.up+1 end,
          keypressed=function() counters.key=counters.key+1 end}
    ''')
    menu = lua.execute(source.read_text())
    menu.attach(lua.globals().screen)
    menu.attach(lua.globals().screen)  # A second require/attach must not double-wrap callbacks.
    lua.execute('''
        screen:init(2400,1080,function() end)
        assert(counters.init==1 and screen.xanadu_start_menu)
        local ui=screen.xanadu_start_menu
        screen:draw()
        local help=ui.layout.help
        screen:mousepressed(help.x+5,help.y+5,1)
        screen:mousereleased(help.x+5,help.y+5,1)
        assert(ui.open and counters.down==0 and counters.up==0,'Help must consume its opening tap')
        screen:draw()
        local content=ui.layout.content
        screen:mousepressed(content.x+20,content.y+content.h-30,1)
        mouse_x=content.x+20; mouse_y=content.y+20; mouse_down=true
        screen:update(0.016)
        mouse_down=false
        screen:mousereleased(mouse_x,mouse_y,1)
        assert(ui.scroll>0,'Dragging anywhere in the help body must scroll')
        assert(counters.down==0 and counters.up==0,'A modal gesture must not reach the game')
        local old_scroll=ui.scroll
        screen:wheelmoved(0,-1)
        assert(ui.scroll>=old_scroll)
        screen:keypressed('escape')
        assert(not ui.open and counters.key==0,'Back closes help before the game sees it')
        screen:keypressed('escape')
        assert(counters.key==1,'Back outside help keeps the original behavior')
        screen:mousepressed(10,500,1); screen:mousereleased(10,500,1)
        assert(counters.down==1 and counters.up==1,'Original start screen remains interactive')

        local settings=ui.layout.settings
        screen:mousepressed(settings.x+5,settings.y+5,1)
        screen:mousereleased(settings.x+5,settings.y+5,1)
        assert(counters.settings==1 and not options.hidden,'Settings must use the existing options view')
        screen:mousepressed(help.x+5,help.y+5,1); screen:mousereleased(help.x+5,help.y+5,1)
        assert(not ui.open,'Controls must not overlay the existing settings modal')
        options.hidden=true
        screen:mousepressed(help.x+5,help.y+5,1); screen:mousereleased(help.x+5,help.y+5,1)
        assert(ui.open)
        screen:draw()
        local close=ui.layout.close
        screen:mousepressed(close.x+5,close.y+5,1)
        local ups=counters.up
        screen:mousereleased(close.x+5,close.y+5,1)
        assert(not ui.open and counters.up==ups,'Closing release must never click through')

        screen:mousepressed(help.x+5,help.y+5,1); screen:mousereleased(help.x+5,help.y+5,1)
        screen:mousepressed(content.x+20,content.y+20,1)
        screen:keypressed('escape')
        ups=counters.up
        screen:mousereleased(content.x+20,content.y+20,1)
        assert(not ui.open and counters.up==ups,'Back during a gesture must still consume its release')

        screen:mousepressed(help.x+5,help.y+5,1)
        viewport_w,viewport_h=1920,1080
        screen:draw()
        ups=counters.up
        screen:mousereleased(help.x+5,help.y+5,1)
        assert(counters.up==ups and not ui.open,'A resize must consume an in-flight menu gesture')
        window._disabled=true
        local downs=counters.down
        help=ui.layout.help
        screen:mousepressed(help.x+5,help.y+5,1)
        screen:mousereleased(help.x+5,help.y+5,1)
        assert(not ui.open and counters.down==downs+1,'Navigation must disable the new controls too')
        window._disabled=false

        for _,size in ipairs({{2400,1080},{1920,1080},{1280,800},{1080,2400}}) do
          viewport_w,viewport_h=size[1],size[2]
          screen:draw()
          ui.open=true; screen:draw()
          local p=ui.layout.panel
          assert(p.x>=0 and p.y>=0 and p.x+p.w<=viewport_w and p.y+p.h<=viewport_h)
          assert(ui.layout.content.h>0 and ui.layout.settings.w>=40)
          ui.open=false
        end
        screen:destroy()
        assert(counters.destroy==1 and screen.xanadu_start_menu==nil)
        screen:init(2400,1080,function() end)
        assert(not screen.xanadu_start_menu.open,'Returning to the start screen must create clean UI state')
        destroy_during_update=true
        screen:update(0.016)
        assert(screen.xanadu_start_menu==nil,'The original timer may navigate and destroy the screen during update')
        destroy_during_update=false
        screen:init(2400,1080,function() end)
        destroy_during_draw=true
        screen:draw()
        assert(screen.xanadu_start_menu==nil,'Do not draw a menu after the original screen has been destroyed')
    ''')
    print('PASS: actual Lua help, drag/wheel scrolling, modal isolation, Back, existing settings, close-release isolation, four aspect ratios and re-entry lifecycle')


if __name__ == '__main__':
    main()
