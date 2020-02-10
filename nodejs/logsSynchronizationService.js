const private = {

    requestsQueue: require('./requestQueueService')(),
    logFilesService: require('./logFilesService')(),

    wd: require("wd"),
    wdAndroid: require('wd-android'),
    fs: require("fs"),
    _: require('underscore'),
    path: require('path'),
    
    path: require('path'),
    request: require('request'),

    driver: null,
    wda: null,
    awsManagement: null,
    captchaService: null,

    constants: {
        pathUsersLogs: 'userslogs/',
        pathPrefixScreen: 'captchas/o',
        pathPrefixCaptcha: 'captchas/c',

        NO_STOP_GMOTION: 'nostopgm',
        NO_START_GMOTION: 'nostartgm',
    },

    desired: {
        "platformName": "android",
        "deviceName": "device",
        "appPackage": "dji.go.v4",
        "appActivity": "dji.pilot.main.activity.DJILauncherActivity",
        "app": "D:\\Projects\\djilogs\\DJI GO 4.apk",
        "appWaitActivity": "dji.pilot2.main.activity.DJIMainFragmentActivity",
        //"appWaitActivity": "dji.pilot2.main.activity.DJILegalAgreement"
    },

    serverConfig: {
        host: 'localhost',
        port: 4725
    },

    state: null,

    init: function () {
        this.wda = new this.wdAndroid(this.wd);
        this.createDriver();

        this.awsManagement = require('./awsManagementService')(this.logMessage);
        this.captchaService = require('./captchaService')(this.logMessage);

        this.events();
    },
    events: function () {

    },


    createDriver: function () {
        this.driver = this.wda.promiseChainRemote(this.serverConfig);
        return this.driver;
    },
    createContext: function () {
        let context = private.driver
            .init(private.desired)
            .setImplicitWaitTimeout(3000);
        return context;
    },
    login: function (context, login, pass, captcha) {
        if (captcha) {
            context = context
                .elementByXPath("//android.widget.EditText[contains(@text, 'verification code')]")
                .setText(captcha);
        } else {
            context = context
                .elementByXPath("//android.widget.EditText[contains(@text, 'Enter a valid email')]")
                .setText(login);
        }

        return context
            .elementByXPath("//android.widget.EditText[@text='']")
            .setText(pass)

            .setImplicitWaitTimeout(5000)

            .elementByXPath("//android.widget.Button[@text='Login']")
            .tap();
    },
    copyLogsFiles: function (context) {
        private.logMessage('Log files copying started');
        return context
            .then(function () { //base64File
                private.setStage('files-copying');
                return private.logFilesService.copyFilesPromise(private.state, private.logMessage);
            });
    },

    getCaptchaImageElement: function (context) {
        return context
            .elementByXPath("//android.widget.ImageView[contains(@resource-id, 'verification_code')]");
    },
    isCaptcha: function (context) {
        private.logMessage('Captcha checking started');
        return context
            .hasElementByXPath("//android.widget.ImageView[contains(@resource-id, 'verification_code')]");
    },
    isLegalShown: function (context) {
        let legalXPath = "//android.widget.TextView[contains(@resource-id, 'legal_agreement_agree')]";

        return context.elementByXPath(legalXPath).tap()
            .then(function (err, element) {
                private.logMessage('Legal popup shown:', true);
            }, function (err) {
                private.logMessage('Legal popup shown:', false);
            });
    },
    isUpdateShown: function (context) {
        let legalXPath = "//android.widget.TextView[contains(@resource-id, 'popup_dlg_cancel_btn')]";
        return context.elementByXPath(legalXPath).tap()
            .then(function (err, element) {
                private.logMessage('Update popup shown:', true);
            }, function (err) {
                private.logMessage('Update popup shown:', false);
            });
    },
    isIgnorableMessageShown: function (context) {
        let ignorableXPath = "//android.widget.TextView[contains(@text, 'Ignore')]";
        return context.elementByXPath(ignorableXPath).tap()
            .then(function (err, element) {
                private.logMessage('Ignorable popup shown:', true);
            }, function (err) {
                private.logMessage('Ignorable popup shown:', false);
            });
    },

    inputCaptchaAndTryLogin: function (context, taskSolution) {
        private.logMessage('Login after captcha passing');
        return private.login(context, private.state.login, private.state.password, taskSolution)
            .then(function () {
                return private.isPasswordIncorrectDialogue(context);
            })
            .then(function (isPasswordIncorrect) { //here we catch the "Invalid password error"
                private.logMessage('Password is correct:', !isPasswordIncorrect);
                if (isPasswordIncorrect) throw new Error('Incorrect password');
            })
            .then(function () {
                return private.syncFlights(context)
            });
    },
    enterCaptcha: function (context) {
        var captcha = private.getCaptchaImageElement(context);
        private.logMessage('Wait for captcha element loading');

        let unixTime = private.getUnixTime();
        let screenShotPath = '';
        let captchaLocation = null;
        let captchaPath = '';

        let absPath = private.path.join(private.path.dirname(require.main.filename), private.constants.pathPrefixScreen + unixTime);

        return captcha
            .sleep(2000)
            .saveScreenshot(absPath)
            .then(function (spath) {
                screenShotPath = spath;
                private.logMessage('Save the screenshot by path:', spath);
                return captcha.getLocation();
            })
            .then(function (location) {
                captchaLocation = location;
                private.logMessage('Captcha location:', location.x, location.y);
                return captcha.getSize();
            })
            .then(function (size) {
                private.logMessage('Captcha size:', size.width, size.height);
                captchaPath = private.path.join(private.path.dirname(require.main.filename), private.constants.pathPrefixCaptcha + unixTime + '.png');
                return private.captchaService.cropCaptcha(screenShotPath, captchaPath, size, captchaLocation);
            })
            .then(function () {
                private.logMessage('Load captcha fragment from file');
                let data = private.fs.readFileSync(captchaPath);
                let base64data = new Buffer(data).toString('base64');

                return private.captchaService.solveCaptcha(captcha, base64data);
            })
            .then(function (taskSolution) {
                private.logMessage('Anticaptcha returned the solution:', taskSolution);
                return private.inputCaptchaAndTryLogin(context, taskSolution);
            });
    },

    openFlights: function (context) {
        return context
            .elementByXPath("//android.widget.ImageView[@resource-id='dji.go.v4:id/main_device_more']")
            .tap()

            .elementByXPath("//android.widget.TextView[@text='Flight Record']")
            .tap();
    },
    isSyncFlights: function (context) {
        private.logMessage('Sync flights check function called');
        return context
            .sleep(2000)
            .hasElementByXPath("//android.widget.TextView[contains(@text, 'Synchronizing')]");
    },
    waitSyncFlights: function (context) {
        private.logMessage('Wait for flights sync');

        return private.isSyncFlights(context)
            .then(function (isSyncInProgress) {
                private.logMessage('Is flights sync in progress:', isSyncInProgress);
                if (isSyncInProgress) {
                    return private.waitSyncFlights(context);
                }
            });
    },
    syncFlights: function (context) {
        private.logMessage('Sychronizing the flights');
        return context
            .sleep(5000)
            .elementByXPath("//android.widget.ImageView[@resource-id='dji.go.v4:id/flightrecord_view_refresh']")
            .tap()
            .then(function () {
                return private.waitSyncFlights(context);
            });
    },

    isPasswordIncorrectDialogue: function (context) {
        let dialogTitleXPath = "//android.widget.TextView[contains(@resource-id, 'dialog_title')]";
        return context
            .sleep(4000)
            .elementByXPathOrNull(dialogTitleXPath)
            .then(function (element) {
                let isDisplayed = element !== null;
                private.logMessage('Is dialogues displayed:', isDisplayed);
                if (isDisplayed) {
                    return element.text().then(function (text) {
                        private.logMessage('Dialogue text:', text);
                        return text && text.trim().toLowerCase() === "invalid password";
                    });
                } else {
                    return false;
                }
            });
    },

    getUnixTime: function () {
        return new Date().getTime();
    },
    logMessage: function () {
        var message = '[' + new Date().toUTCString() + '] ';
        for (var i in arguments) {
            message += arguments[i] + ' ';
        }
        message = message.trim();

        if (private.state) {
            if (!private.state.log) private.state.log = [];
            private.state.log.push(message);
        }

        console.log(message);
    },
    setStage: function (stage) {
        if (private.state && stage) {
            private.state.stage = stage;
        }
    },

    handleSuccess: function (obj) {
        obj = obj || {};
        obj.status = 'ok';

        if (private.state.async) {
            private.sendAsyncResponse(private.state.success, obj, function () {
                private.requestsQueue.dequeue(private.state);
            });
        } else {
            if (private.writeResponse(obj)) {
                private.finishReponse();
            }
        }
    },
    handleError: function (error, secondaryError) {
        if (private.state.async) {
            private.sendAsyncResponse(private.state.error, {
                status: 'error',
                error: error,
                secondaryError: secondaryError,
                message: error.message,
                stage: private.state.stage,
                log: private.state.log
            }, function () {
                private.requestsQueue.dequeue(private.state);
            });
        } else {
            if (private.writeResponse({
                status: 'error',
                error: error,
                secondaryError: secondaryError,
                message: error.message,
                stage: private.state.stage,
                log: private.state.log
            })) {
                private.finishReponse();
            }
        }

        
    },
    handleAsyncResponse: function () {
        if (private.writeResponse({
            status: 'ok',
            message: 'Your request is processing'
        })) {
            private.state.res.end(); //we need to free this response
            private.state.res = null; //and set null to response to avoid using it again
            private.state.req = null;
        }
    },

    writeResponse: function (response) {
        if (private.state && private.state.res && !private.state.ended) {
            private.state.res.write(JSON.stringify(response));
            return true;
        } else {
            return false;
        }
    },
    sendAsyncResponse: function (endpoint, response, callback) {
        if (private.state && !private.state.ended) {
            private.request.post({
                headers: { 'content-type': 'application/json' },
                url: endpoint,
                body: JSON.stringify(response)
            }, callback);
            return true;
        } else {
            return false;
        }
    },
    finishReponse: function () {
        if (!private.state.immediate) {
            private.requestsQueue.dequeue(private.state);
        } else {
            private.state.ended = true;
            return private.state.res.end();
        }
    },
    hasAction: function (action) {
        return private.state.actions && private.state.actions.indexOf(action) >= 0;
    }
};

module.exports = function (server) {
    private.init(server);

    return {
        start: function (state) {

            private.state = state;
            if (private.state.async) {
                private.handleAsyncResponse();
            }

            let context = null;
            let appiumHangTimeout = null;
            let isDriverReCreated = false;

            let reInitContext = function () {
                if (!isDriverReCreated) {
                    private.logMessage('Re-initializing the context...');
                    private.createDriver();
                    context = private.createContext();
                    isDriverReCreated = true;
                } else {
                    private.logMessage('The context already re-initialized');
                }
            };

            let initialAction = function () {
                appiumHangTimeout = setTimeout(function () {
                    private.logMessage('Executing the Appium shutdown script');
                    private.awsManagement.shutdownAppiumPromise(private.serverConfig.port)
                        .then(function () {
                            reInitContext();
                        });
                }, 30000);

                context = private.createContext();
                context.catch(function (error) {
                    clearTimeout(appiumHangTimeout);

                    private.logMessage('Error has occured:', error, 'Trying to re-initialize the process.');

                    reInitContext();

                    return context;
                });

                return context;
            };

            private.logMessage('Starting process...');
            let promise = null;

            if (private.hasAction(private.constants.NO_START_GMOTION)) {
                private.logMessage('Genymotion instance starting cancelled by action:', private.constants.NO_START_GMOTION);
            } else {
                private.setStage('instance-start');

                promise = private.awsManagement.startPromise()
                    .then(function () {
                        private.logMessage('Wait for 2 seconds');
                        return private.awsManagement.waitTime(2000);
                    });
            }

            promise = promise ? promise.then(initialAction) : initialAction();
            promise
                .then(function () {
                    clearTimeout(appiumHangTimeout);

                    private.logMessage('Started login for:', private.state.login);

                    private.setStage('logs-synchronization');
                    return private.isIgnorableMessageShown(context);
                })
                .then(function () {
                    return private.isIgnorableMessageShown(context);
                })
                .then(function () {
                    return private.isLegalShown(context);
                })
                .then(function () {
                    return private.isIgnorableMessageShown(context);
                })
                .then(function () {
                    return private.isUpdateShown(context);
                })
                .then(function () {
                    return private.isIgnorableMessageShown(context);
                })
                .then(function () {
                    return private.openFlights(context);
                })
                .then(function () {
                    return private.isIgnorableMessageShown(context);
                })
                .then(function () {
                    return private.login(context, private.state.login, private.state.password);
                })
                .then(function () {
                    private.logMessage('Login screen passed, checking login result');
                    return private.isPasswordIncorrectDialogue(context);
                })
                .then(function (isPasswordIncorrect) { //here we catch the "Invalid password error"
                    private.logMessage('Password is correct:', !isPasswordIncorrect);
                    if (isPasswordIncorrect) throw new Error('Incorrect password');
                })
                .then(function () {
                    return private.isCaptcha(context);
                })
                .then(function (isCaptchaDisplayed) {
                    private.logMessage('Is captcha displayed:', isCaptchaDisplayed);
                    if (isCaptchaDisplayed) {
                        return private.enterCaptcha(context);
                            
                    } else {
                        return private.syncFlights(context);
                    }
                })
                .then(function () {
                    return private.isIgnorableMessageShown(context);
                })
                .then(function () {
                    return private.copyLogsFiles(context);
                })
                .then(function (result) {
                    private.driver.quit();

                    if (private.hasAction(private.constants.NO_STOP_GMOTION)) {
                        private.logMessage('Genymotion instance stopping cancelled by action:', private.constants.NO_STOP_GMOTION);
                        return private.handleSuccess(result);
                    } else {
                        return private.awsManagement.stopPromise().then(
                            function () {
                                return private.handleSuccess(result);
                            },
                            function (stopError) {
                                return private.handleError(stopError);
                            });
                    }
                })
                .catch(function (error) {
                    private.driver.quit();

                    if (private.hasAction(private.constants.NO_STOP_GMOTION)) {
                        private.logMessage('Genymotion instance stopping cancelled by action:', private.constants.NO_STOP_GMOTION);
                        return private.handleError(error);
                    } else {
                        return private.awsManagement.stopPromise().then(
                            function () {
                                return private.handleError(error);
                            },
                            function (stopError) {
                                return private.handleError(error, stopError);
                            });
                    }
                });
                
        },

        launchGenymotion: function (state) {
            private.state = state;
            if (private.state.async) {
                private.handleAsyncResponse();
            }

            private.logMessage('Starting Genymotion instance');
            private.awsManagement.startPromise()
                .then(function () {
                    return private.handleSuccess();
                })
                .catch(function (error) {
                    return private.handleError(error);
                });
        },

        shutdownGenymotion: function (state) {
            private.state = state;
            if (private.state.async) {
                private.handleAsyncResponse();
            }

            private.logMessage('Stopping Genymotion instance');
            private.awsManagement.stopPromise()
                .then(function () {
                    return private.handleSuccess();
                })
                .catch(function (error) {
                    return private.handleError(error);
                });
        },

        statusGenymotion: function (state, type) {
            private.state = state;
            if (private.state.async) {
                private.handleAsyncResponse();
            }

            private.logMessage('Getting Genymotion status');
            private.awsManagement.statusPromise(type)
                .then(function (data) {
                    return private.handleSuccess({ data: data });
                })
                .catch(function (error) {
                    return private.handleError(error);
                });
        },

        getCaptchaPaths: function (state) {
            private.state = state;
            if (private.state.async) {
                private.handleAsyncResponse();
            }

            return private.handleSuccess({
                screenshots: private.path.join(private.path.dirname(require.main.filename), private.constants.pathPrefixScreen),
                captchas: private.path.join(private.path.dirname(require.main.filename), private.constants.pathPrefixCaptcha)
            });
        },

        testCaptchaSolving: function (state, data) {
            private.state = state;
            if (private.state.async) {
                private.handleAsyncResponse();
            }

            private.captchaService.solveCaptcha(null, data)
                .then(function (solution) {
                    return private.handleSuccess({
                        solution: solution
                    });
                })
                .catch(function (error) {
                    return private.handleError(error);
                });
        }
    };
};