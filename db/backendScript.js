'use strict';
const express = require('express');
const crypto = require('crypto');
//var cryptoRandomuuid = require("crypto-randomuuid");
const cors = require('cors');
//const rateLimit = require("express-rate-limit");
//const bodyParser = require('body-parser');
var fs = require('fs');
var util = require('util');
const mysql = require('mysql');
const multer = require('multer');
const path = require('path');
const formidable = require('formidable');

function API(app) {
	let date_ob = new Date();
	let date = ("0" + date_ob.getDate()).slice(-2);
	let month = ("0" + (date_ob.getMonth() + 1)).slice(-2);
	let year = date_ob.getFullYear();
	let hours = date_ob.getHours();
	let minutes = date_ob.getMinutes();
	let seconds = date_ob.getSeconds();

	var datefull = year + "-" + month + "-" + date + "--" + hours + "_" + minutes + "_" + seconds

	var log_file = fs.createWriteStream(__dirname + '/logs/SMMLog/' + datefull + '-debug.log', { flags: 'w' });
	var log_stdout = process.stdout;

	const connection = mysql.createPool({
		connectionLimit: 100,
		user: 'test',
		password: 'test',
		host: 'localhost',
		port: '3306',
		database: 'FloriansDB',
	});
	connection.getConnection((err) => {
		if (err) throw err;
		apilog('Connected to FlorianDB Database!');
	});

	function init() {

		app.use(cors());
		app.use(express.json());

		app.get('/smm1/searchLevelsByName/:levelname', (req, res) => {
		  searchLevelByName(req.params.levelname, res);
		});

	}

	function searchLevelByName(searchParam, res) {
	  const query = `
		SELECT 
		l.url as "url",
		l.course_name as "name",
		l.creator_nnid as "creator",
		l.id as "levelid",
		l.creator_pid as "creatorid",
		l.clears as "clears",
		l.failures as "failures",
		l.total_attempts as "total_attempts",
		l.clears_total_attempts as "clearrate",
		l.upload_time as "uploadTime",
		l.world_record_best_time_ms as "world_record_ms",
		l.world_record_best_time_player_nnid as "world_record_holder_nnid",
		l.stars as "stars"
		FROM Levels l
		WHERE l.course_name LIKE ?
		ORDER BY l.EntryID DESC LIMIT 10`;

	  connection.query(query, [searchParam], (error, results) => {
		if (error) {
		  res.status(500).json({ error: 'Error Executing SQL' });
		} else {
		  res.json(results);
		}
	  });
	}

	function apilog(arg) {
		log_file.write(util.format(arg) + '\n');
		log_stdout.write(util.format(arg) + '\n');
		//console.log(arg);
	}

	connection.on('error', function (err) {
		hook.send("API Crashed/Errored!");
		apilog('Caught this error: ' + err.toString());
		connection.release();

		do {
			connection.getConnection((err) => {
				//if (err) throw err;
				if (!err) {
					apilog('Connected!');
				}
			});
		} while (err)
		init()
	});


	init()
}

module.exports = API;