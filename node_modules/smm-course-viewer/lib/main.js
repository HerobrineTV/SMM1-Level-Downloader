/* Requires */
const fs = require("fs");

/* smm-course-viewer */
const _SmmCourseViewer = require("./SmmCourseViewer.js");


/**
 * @module SmmCourseViewer
 * This class can read and interpret a course (*.cdt) of Super Mario Maker game. (NodeJS)
 * 
 * @author Leonardo Mauro <leo.mauro.desenv@gmail.com> (http://leonardomauro.com/)
 * @link https://github.com/leomaurodesenv/smm-course-viewer GitHub
 * @license https://opensource.org/licenses/GPL-3.0 GNU Public License (GPLv3)
 * @copyright 2019 Leonardo Mauro
 * @package smm-course-viewer
 * @extends _SmmCourseViewer
 * @access public
 */
class SmmCourseViewer extends _SmmCourseViewer {

    /**
     * @method module:SmmCourseViewer
     * Constructor of class
     * @extends _SmmCourseViewer
     * @var {Object} course
     * @var {Array[Object]} objects
     * @instance
     * @access public
     * @return {this}
     */
    constructor() {
        /* parent extend */
        super();
    }

    /**
     * @method module:_SmmCourseViewer::_readBinaryFile
     * Read the binary file (~ NodeJS version)
     * @arg {File} _file               .cdt File
     * @arg {Function} _callback       Callback function
     * @access private
     * @return {null}
     * @override
     */
    _readBinaryFile(_file, _callback) {
        const $this = this;
        /* load file */
        fs.readFile(_file, function(err, raw) {
            if(err) {
                console.error("SmmCourseViewer: Error: Read the binary!");
                console.error(err);
                _callback(false);
            }
            else {
                const rawHex = $this._raw2hex(raw);
                $this._interpreterBinaryFile(rawHex, _callback);
            }
        });
    }

}


/* Module export */
module.exports = new SmmCourseViewer();