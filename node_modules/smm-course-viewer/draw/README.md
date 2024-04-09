# smm-course-viewer: Drawing Documentation

## Summary

-   [Files](#files)
    -   Files Description

-   [Documentation](#documentation)
    -   Classes
    -   Classes Documentation

--- 
## Files

```json
/
  |__ index.html
/draw/
  |__ Draw.js
  |__ BlockDraw.js
  |__ MonsterDraw.js
```

### Files Description

-   [/index.html](/index.html): a display example of the `smm-course-viewer`.
-   [/lib/Draw.js](/lib/Draw.js): draw the course into an HTML Canvas element.
-   [/lib/BlockDraw.js](/lib/BlockDraw.js): draw the block objects.
-   [/lib/MonsterDraw.js](/lib/MonsterDraw.js): draw the monster objects.

---
## Documentation

### Classes

-   [Draw](#draw): main class.
-   [BlockDraw](#blockdraw): draw the block objects.
-   [MonsterDraw](#monsterdraw): draw the monster objects.

### Classes Description

#### `Draw`   
This class draw the course into an HTML Canvas element.   

**Variables**
```javascript
@arg {String}  _element      HTML element
@arg {Integer} _course       Course data
@arg {Integer} _objects      Course objects
@arg {Integer} _sizeBase     Objects size
```

**Methods** 
```javascript
@method Draw::_init()
// Init the (HTML) Canvas Element
@access private
@return {null}
```

```javascript
@method Draw::_drawBackground(_X, _Y)
// Draw the background
@arg {Integer} _x           Width block counting
@arg {Integer} _Y           Height block counting
@access private
@return {null}
```

```javascript
@method Draw::_drawObjects(_objects)
// Draw the objects
@arg {Array[Object]} _objects    Course objects
@access private
@return {null}
```

```javascript
@method Draw::_drawText(_x, _y, text)
// Draw a text
@arg {Integer} _x        X-axis position
@arg {Integer} _y        Y-axis position
@arg {Integer} text      Text
@access private
@return {null}
```

```javascript
@method Draw::_drawObjectFromTheme(_theme, courseObject)
// Draw a object from a theme
@arg {Object} _theme            BlockDraw, or MonsterDraw
@arg {Object} courseObject      CourseObject
@access private
@return {null}
```

```javascript
@method Draw::_drawObjectFromTheme(_objectPaint)
// Draw a object from a theme
@arg {Object} _objectPaint      Object characteristics
@access private
@return {null}
```

---
#### `BlockDraw`   
This class draw the blocks from [/layout/draw/titleset/](/layout/draw/titleset/).   

**Variables**
```javascript
@var {String} _gameMode      Game mode
@var {String} _gameTheme     Game theme
@var {Object} _defitions     Block draw definitions
```

**Methods** 
```javascript
@method BlockDraw::getTheme()
// Return the theme image
@access public
@return {HTML Element}
```

```javascript
@method BlockDraw::getThemeSize()
// Return the theme size
@access public
@return {Integer}
```

```javascript
@method BlockDraw::getDef(_type)
// Return the block definitions
@arg {Integer} _type        Object type
@access public
@return {Object}
```

```javascript
@method BlockDraw::hasDraw(_type)
// Check if this type has a draw
@arg {Integer} _type        Object type
@access public
@return {Object}
```

```javascript
@method BlockDraw::_autoComplete3x4(tt, limit)
// Complete tt for extend objects
@static
@arg {Object}  tt       Templates position
@arg {Integer} limit    How many positions?
@access private
@return {Object}
```

```javascript
@method BlockDraw::_extend3x4objects(x, y, width, height, ttInit)
// Auxiliar function to extend objects
@arg {Integer} x            X-axis position
@arg {Integer} y            Y-axis position
@arg {Integer} width        Object width
@arg {Integer} height       Object height
@arg {Object}  ttInit       Templates position
@static
@access private
@return {Array[Object]}
```

---
#### `MonsterDraw`   
This class draw the monsters from [/layout/draw/monster/](/layout/draw/monster).   

**Variables**
```javascript
@var {String} _gameMode      Game mode
@var {String} _gameTheme     Game theme
@var {Object} _defitions     Monster draw definitions
```

**Methods** 
```javascript
@method MonsterDraw::getTheme()
// Return the theme image
@access public
@return {HTML Element}
```

```javascript
@method MonsterDraw::getThemeSize()
// Return the theme size
@access public
@return {Integer}
```

```javascript
@method MonsterDraw::getDef(_type)
// Return the monster definitions
@arg {Integer} _type        Object type
@access public
@return {Object}
```

```javascript
@method MonsterDraw::hasDraw(_type)
// Check if this type has a draw
@arg {Integer} _type        Object type
@access public
@return {Object}
```

```javascript
@method MonsterDraw::_extendForObjects(extend, weight, height, xT, yT, x, y)
// Auxiliar function to extend objects
@arg {Object}  extend       Object extend
@arg {Integer} width        Object width
@arg {Integer} height       Object height
@arg {Integer} xT           Theme x-axis position
@arg {Integer} yT           Theme y-axis position
@arg {Integer} x            X-axis position addition
@arg {Integer} y            Y-axis position addition
@static
@access private
@return {Array[Object]}
```

---
## Also look ~

-   Create by Leonardo Mauro ~ [leomaurodesenv](https://github.com/leomaurodesenv/)
-   GitHub: [smm-course-viewer](https://github.com/leomaurodesenv/smm-course-viewer)
