# smm-course-viewer: Code Documentation

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
/lib/
  |__ Course.js
  |__ CourseObject.js
  |__ BlockObject.js
  |__ MonsterObject.js
  |__ SmmCourseViewer.js
  |__ main.js
/test/
  |__ test.js
```

### Files Description

-   [/index.html](/index.html): a display example of the `smm-course-viewer`.
-   [/lib/Course.js](/lib/Course.js): course structure.
-   [/lib/CourseObject.js](/lib/CourseObject.js): course object structure.
-   [/lib/BlockObject.js](/lib/BlockObject.js): block object structure.
-   [/lib/MonsterObject.js](/lib/MonsterObject.js): monster object structure.
-   [/lib/SmmCourseViewer.js](/lib/SmmCourseViewer.js): interpret the course data from a .cdt file. (JavaScript)
-   [/lib/main.js](/lib/main.js): interpret the course data from a .cdt file. (NodeJS)
-   [/test/test.js](/test/test.js): NodeJS script of testing.

---
## Documentation

### Classes

-   [Course](#course): course structure.
-   [CourseObject](#courseobject): course object structure.
-   [BlockObject](#blockobject): block object structure.
-   [MonsterObject](#monsterobject): monster object structure.
-   [\_SmmCourseViewer](#_smmcourseviewer): course interpreter. (JavaScript)
-   [SmmCourseViewer](#smmcourseviewer): course interpreter. (NodeJS)

### Classes Description

#### `Course`   
This class is a struct for course data.   

**Variables**
```javascript
@var {Integer} version       SMM version
@var {Integer} checksum      File (.cdt) checksum
@var {String} year           Creation time (year)
@var {String} month          Creation time (month)
@var {String} day            Creation time (day)
@var {String} hour           Creation time (hour)
@var {String} minute         Creation time (minute)
@var {String} name           Name
@var {String} mode           Mode: 'M1', 'M3', 'MW', 'WU'
@var {Integer} theme         Theme: 0, 1, 2, 3, 4, 5
@var {String} themeName      Theme: 'overworld', 'underground', 'castle', 'airship', 'water', 'ghostHouse'
@var {Integer} timeLimit     Time limit
@var {Integer} scroll        Scroll: 0, 1, 2, 3
@var {Integer} scrollName    Scroll: 'none', 'slow', 'medium', 'fast'
@var {Integer} flags         Unknown -
@var {Integer} width         Width size
@var {Integer} widthBlock    Width Blocks count
@var {Integer} heightBlock   Height Blocks count
@var {Integer} objectCount   Object count
```

**Methods** 
```javascript
@method Course::getHtml()
// Return a HTML with course information.
@access public
@return {String}
```

---
#### `CourseObject`   
This class is a struct for course objects.   

**Variables**
```javascript
@var {Integer} x                 Axis-x position
@var {Integer} z                 Axis-x position
@var {Integer} y                 Axis-x position
@var {Integer} width             Width
@var {Integer} height            Height
@var {Integer} flags             Define sub-type
@var {Integer} childFlags        Unknow -
@var {Integer} extendedData      Define direction
@var {Integer} type              Type
@var {Integer} childType         Child type
@var {Integer} linkId            to Pipes and Rails
@var {Integer} effect            Effect
@var {Integer} transform         Tranformation
@var {Integer} childTransform    Child tranformation
```

**Methods** 
```javascript
@method CourseObject::getType()
// Return the object class name.
@access public
@return {String}
```

```javascript
@method CourseObject::isBlock(_type)
// Check if is a block.
@arg {Integer} _type     Game type
@static
@access public
@return {Boolean}
```

---
#### `BlockObject`   
This class represents the block objects.   

**Variables**
```javascript
@extends CourseObject
@var {Object}  name             Object name
@var {Object}  names            Objects name
@var {Object}  codes            Objects code
```

**Methods** 
```javascript
@method BlockObject::isBlock(_type)
// Check if is a block
@arg {Integer} _type     Game type
@static
@access public
@return {Boolean}
```

---
#### `MonsterObject`   
This class represents the monster objects.   

**Variables**
```javascript
@extends CourseObject
@var {Object}  name             Monster name
@var {Object}  names            Monsters name
@var {Object}  codes            Monsters code
@var {Integer} subType          Subtype
@var {Boolean} wing             Does it have wings?
@var {Integer} size             Sizes: 1 'normal', 2 'big ~ mushroom'
@var {Integer} direction        Unknown -
```

**Methods** 
```javascript
@method MonsterObject::isBlock(_type)
// Check if is a block
@arg {Integer} _type     Game type
@static
@access public
@return {Boolean}
```
```javascript
@method MonsterObject::_extAttributes(_objectData)
// Extends the attributes class
@arg {Object} _objectData    Object data
@access private
@return {Object}
```

---
#### `_SmmCourseViewer`   
This class can read and interpret a course (.cdt) of Super Mario Maker game. (Vanilla JS)   

**Variables**
```javascript
@var {Object}        course     Course data
@var {Array[Object]} objects    Course objects
```

**Methods**
```javascript
@method _SmmCourseViewer::read(_file, _callback)
// Read a file to extract course data
@arg {File} _file               .cdt file
@arg {Function} _callback       Callback function
@access public
@return {this}
```
```javascript
@method _SmmCourseViewer::_readBinaryFile(_file, _callback)
// Read the binary file
@arg {File} _file               .cdt File
@arg {Function} _callback       Callback function
@access private
@return {null}
```

```javascript
@method _SmmCourseViewer::_raw2hex(_raw)
// Convert raw blob to hexadecimal array
@arg {String} _raw              Raw .cdt file
@access private
@return {Array[String]}
```

```javascript
@method _SmmCourseViewer::_rawHex2hex(_rawHex, _pos, _size)
// Convert hexadecimal array to hexadecimal string
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Integer} _pos             Index of the vector
@arg {Integer} _size            Length of the vector
@access private
@return {String}
```

```javascript
@method _SmmCourseViewer::_rawHex2sint(_rawHex, _pos, _size)
// Convert hexadecimal array to signed integer
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Integer} _pos             Index of the vector
@arg {Integer} _size            Length of the vector
@access private
@return {Integer}
```

```javascript
@method _SmmCourseViewer::_rawHex2uint(_rawHex, _pos, _size)
// Convert hexadecimal array to unsigned integer
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Integer} _pos             Index of the vector
@arg {Integer} _size            Length of the vector
@access private
@return {Integer}
```

```javascript
@method _SmmCourseViewer::_rawHex2string(_rawHex, _pos, _size)
// Convert hexadecimal array to string
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Integer} _pos             Index of the vector
@arg {Integer} _size            Length of the vector
@access private
@return {String}
```

```javascript
@method _SmmCourseViewer::_interpreterBinaryFile(_rawHex, _callback)
// Interpreter the binary file
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Function} _callback       Callback function
@access private
@return {null}
```

```javascript
@method _SmmCourseViewer::_courseInterpreter(_rawHex, _callback)
// Interpreter of course data
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Function} _callback       Callback function
@access private
@return {null}
```

```javascript
@method _SmmCourseViewer::_objectInterpreter(_rawHex, _pos)
// Interpreter of object data
@arg {Array[String]} _rawHex    Raw hexadecimal array
@arg {Integer} _pos             Index of the object
@access private
@return {Object}
```

---
#### `SmmCourseViewer`   
This class can read and interpret a course (.cdt) of Super Mario Maker game. (NodeJS)   

**Variables**
```javascript
@extends _SmmCourseViewer
```

**Methods**
```javascript
@method _SmmCourseViewer::_readBinaryFile(_file, _callback)
// Read the binary file (~ NodeJS version)
@arg {File} _file               .cdt File
@arg {Function} _callback       Callback function
@access private
@return {null}
@override
```

---
## Also look ~

-   Create by Leonardo Mauro ~ [leomaurodesenv](https://github.com/leomaurodesenv/)
-   GitHub: [smm-course-viewer](https://github.com/leomaurodesenv/smm-course-viewer)
