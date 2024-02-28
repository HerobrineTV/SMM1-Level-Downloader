/* Requires */
try {
    /*global Course CourseObject BlockObject MonsterObject */
    Course = require("./Course.js");
    CourseObject = require("./CourseObject.js");
    BlockObject = require("./BlockObject.js");
    MonsterObject = require("./MonsterObject.js");
} catch(err) {}


/**
 * @module _SmmCourseViewer
 * This class can read and interpret a course (*.cdt) of Super Mario Maker game. (Vanilla JS)
 * 
 * @author Leonardo Mauro <leo.mauro.desenv@gmail.com> (http://leonardomauro.com/)
 * @link https://github.com/leomaurodesenv/smm-course-viewer GitHub
 * @license https://opensource.org/licenses/GPL-3.0 GNU Public License (GPLv3)
 * @copyright 2019 Leonardo Mauro
 * @package smm-course-viewer
 * @access public
 */
class _SmmCourseViewer {

    /**
     * @method module:_SmmCourseViewer
     * Constructor of class
     * @var {Object} course
     * @var {Array[Object]} objects
     * @instance
     * @access public
     * @return {this}
     */
    constructor() {
        /* global variables */
        this.course = null; 
        this.objects = null;
    }

    /**
     * @method module:_SmmCourseViewer::read
     * Read a file to extract course data
     * @arg {File} _file               .cdt file
     * @arg {Function} _callback       Callback function
     * @access public
     * @return {this}
     */
    read(_file, _callback=function(){}) {
        const $this = this;
        $this._file = _file;
        /* start read the file */
        $this._readBinaryFile($this._file, function(err, course, objects) {
            if(err) {
                _callback(true);
            }
            else {
                /* copy and callback */
                $this.course = course; 
                $this.objects = objects;
                _callback(false, course, objects);
            }
        });
    }

    /**
     * @method module:_SmmCourseViewer::_readBinaryFile
     * Read a binary file *cdt
     * @arg {File} _file               .cdt file
     * @arg {Function} _callback       Callback function
     * @access private
     * @return {null}
     */
    _readBinaryFile(_file, _callback) {
        const $this = this;
        var reader = new FileReader();
        /* load file */
        reader.onloadend = function(eventReader) {
            try {
                let raw = reader.result;
                const rawHex = $this._raw2hex(raw);
                $this._interpreterBinaryFile(rawHex, _callback);
            }
            catch(err) {
                console.error("SmmCourseViewer: Error: Read the binary!");
                console.error(err);
                _callback(true);
            }
        };
        reader.readAsArrayBuffer(_file);
    }

    /**
     * @method module:_SmmCourseViewer::_raw2hex
     * Convert raw blob to hexadecimal array
     * @arg {String} _raw              Raw .cdt file
     * @access private
     * @return {Array[String]}
     */
    _raw2hex(_raw) {
        let rawBytes = new Uint8Array(_raw);
        var rawHex = [];
        for (let cycle = 0; cycle < _raw.byteLength; cycle++) {
            let value = rawBytes[cycle].toString(16);
            rawHex.push((value.length === 1) ? "0"+value : value);
        }
        return rawHex;
    }

    /**
     * @method module:_SmmCourseViewer::_rawHex2hex
     * Convert hexadecimal array to hexadecimal string
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Integer} _pos             Index of the vector
     * @arg {Integer} _size            Length of the vector
     * @access private
     * @return {String}
     */
    _rawHex2hex(_rawHex, _pos, _size=1) {
        let hexString = "";
        for(let idx=_pos; idx<_pos+_size; idx++) { hexString += _rawHex[idx]; }
        return hexString;
    }

    /**
     * @method module:_SmmCourseViewer::_rawHex2sint
     * Convert hexadecimal array to signed integer
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Integer} _pos             Index of the vector
     * @arg {Integer} _size            Length of the vector
     * @access private
     * @return {Integer}
     */
    _rawHex2sint(_rawHex, _pos, _size=1) {
        /* extract hexadecimal and define sign rule */
        let hexString = this._rawHex2hex(_rawHex, _pos, _size);
        let hexParsed = parseInt(hexString, 16);
        let hexRule = {
            mask: 0x8 * Math.pow(16, hexString.length-1),
            sub: -0x1 * Math.pow(16, hexString.length)
        };
        /* return value signed */
        if((hexParsed & hexRule.mask) > 0) {
            return (hexRule.sub + hexParsed); /* negative */
        }
        else {
            return hexParsed; /* positive */
        }
    }

    /**
     * @method module:_SmmCourseViewer::_rawHex2uint
     * Convert hexadecimal array to unsigned integer
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Integer} _pos             Index of the vector
     * @arg {Integer} _size            Length of the vector
     * @access private
     * @return {Integer}
     */
    _rawHex2uint(_rawHex, _pos, _size=1) {
        let hexString = this._rawHex2hex(_rawHex, _pos, _size);
        /* force unsigned (>>>) */
        return parseInt(hexString, 16) >>> 0;
    }

    /**
     * @method module:_SmmCourseViewer::_rawHex2string
     * Convert hexadecimal array to string
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Integer} _pos             Index of the vector
     * @arg {Integer} _size            Length of the vector
     * @access private
     * @return {String}
     */
    _rawHex2string(_rawHex, _pos, _size=1) {
        let hexString = this._rawHex2hex(_rawHex, _pos, _size).toString();
        /* convert hex to ascii */
        let asciiVal = hexString.match(/.{1,2}/g).map(function(c){
            return String.fromCharCode(parseInt(c, 16));
        }).join("");
        /* remove null digit (ascii = 0) */
        let str = asciiVal.match(/[\x01-\x7F]/g).join("");
        return str;
    }

    /**
     * @method module:_SmmCourseViewer::_interpreterBinaryFile
     * Interpreter the binary file
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Function} _callback       Callback function
     * @access private
     * @return {null}
     */
    _interpreterBinaryFile(_rawHex, _callback) {
        const $this = this;
        $this._courseInterpreter(_rawHex, function(course) {
            var objects = [];
            /* read objects - max 2600 objects */
            for (let i = 0; i < course.objectCount; i++) {
                let pos = 0xF0 + (32 * i);
                let courseObject = $this._objectInterpreter(_rawHex, pos),
                    type = courseObject.type;
                /* is Block? or Monster? */
                let typedObject = (BlockObject.is(type)) ? new BlockObject(courseObject):
                                  (MonsterObject.is(type)) ? new MonsterObject(courseObject) : courseObject;
                objects.push(typedObject);
            }
            _callback(false, course, objects);
        });
    }

    /**
     * @method module:_SmmCourseViewer::_courseInterpreter
     * Interpreter of course data from the binary file
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Function} _callback       Callback function
     * @access private
     * @return {null}
     */
    _courseInterpreter(_rawHex, _callback) {
        /* interpreter */
        let version = this._rawHex2uint(_rawHex, 0, 8);
        let checksum = this._rawHex2uint(_rawHex, 0x08, 4);
        let year = this._rawHex2uint(_rawHex, 0x10, 2);
        let month = this._rawHex2uint(_rawHex, 0x12, 1);
        let day = this._rawHex2uint(_rawHex, 0x13, 1);
        let hour = this._rawHex2uint(_rawHex, 0x14, 1);
        let minute = this._rawHex2uint(_rawHex, 0x15, 1);
        let name = this._rawHex2string(_rawHex, 0x28, 66);
        let mode = this._rawHex2string(_rawHex, 0x6A, 2);
        let themesMap = {0:"overworld", 1:"underground", 2:"castle", 3:"airship", 4:"water", 5:"ghostHouse"};
        let theme = this._rawHex2uint(_rawHex, 0x6D, 1);
        let themeName = themesMap[theme];
        let timeLimit = this._rawHex2uint(_rawHex, 0x70, 2);
        let scrollMap = {0:"none", 1:"slow", 2:"medium", 3:"fast"};
        let scroll = this._rawHex2uint(_rawHex, 0x72, 1);
        let scrollName = scrollMap[scroll];
        let flags = this._rawHex2uint(_rawHex, 0x73, 1);
        let areaWidth = this._rawHex2uint(_rawHex, 0x74, 4);
        let hexMiiData = this._rawHex2hex(_rawHex, 0x78, 60); /* usually nothing */
        let objectCount = this._rawHex2uint(_rawHex, 0xEC, 4);
        /* course object */
        let objectData = {
            "version":version, "checksum":checksum, "year":year, "month":month, "day":day, 
            "hour":hour, "minute":minute, "name":name, "mode":mode, "theme":theme, "themeName":themeName, 
            "timeLimit":timeLimit, "scroll":scroll, "scrollName":scrollName, "flags":flags, 
            "areaWidth":areaWidth, "hexMiiData":hexMiiData, "objectCount":objectCount,
        };
        let course = new Course(objectData);
        _callback(course);
    }

    /**
     * @method module:_SmmCourseViewer::_objectInterpreter
     * Interpreter of object data from the binary file
     * @arg {Array[String]} _rawHex    Raw hexadecimal array
     * @arg {Integer} _pos             Index of the object
     * @access private
     * @return {Object}
     */
    _objectInterpreter(_rawHex, _pos) {
        /* interpreter */
        let x = this._rawHex2uint(_rawHex, _pos, 4);
        let z = this._rawHex2uint(_rawHex, _pos + 0x04, 4);
        let y = this._rawHex2sint(_rawHex, _pos + 0x08, 2);
        let width = this._rawHex2sint(_rawHex, _pos + 0x0A, 1);
        let height = this._rawHex2sint(_rawHex, _pos + 0x0B, 1);
        let flags = this._rawHex2uint(_rawHex, _pos + 0x0C, 4);
        let childFlags = this._rawHex2uint(_rawHex, _pos + 0x10, 4);
        let extendedData = this._rawHex2uint(_rawHex, _pos + 0x14, 4);
        let type = this._rawHex2sint(_rawHex, _pos + 0x18, 1);
        let childType = this._rawHex2sint(_rawHex, _pos + 0x19, 1);
        let linkId = this._rawHex2sint(_rawHex, _pos + 0x1A, 2);
        let effect = this._rawHex2sint(_rawHex, _pos + 0x1C, 2);
        let transform = this._rawHex2sint(_rawHex, _pos + 0x1E, 1);
        let childTransform = this._rawHex2sint(_rawHex, _pos + 0x1F, 1);
        /* course object */
        x = parseInt(x/160, 10); z = parseInt(z/160, 10); y = parseInt(y/160, 10); /* normalization */
        let objectData = {
            "x":x, "z":z, "y":y, "width":width, "height":height, 
            "flags":flags, "childFlags":childFlags, "extendedData":extendedData,
            "type":type, "childType":childType, "linkId":linkId, "effect":effect, 
            "transform":transform, "childTransform":childTransform,
        };
        var courseObject = new CourseObject(objectData);
        return courseObject;
    }
}

/* 
 * if is Node.js ~ 
 * module.exports 
 */
try {
    module.exports = _SmmCourseViewer;
} catch(err) {}