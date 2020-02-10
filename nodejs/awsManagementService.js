const private = {

    AWS: require('aws-sdk'),

    ec2: null,
    exec: null,

    constants: {

    },

    paramsInstance: {
        InstanceIds: [ /* required */
            'i-0acf8e9eb06bd2e5d', // Genymotion instance ID
            //'i-0ef7ac756260f9be0', // test instance ID
        ],
    },

    init: function (logFunction) {
        this.AWS.config.update({ region: 'eu-central-1' });
        this.ec2 = new this.AWS.EC2();
        this.exec = require('child_process').exec;

        this.logMessage = logFunction;

        this.events();
    },
    events: function () {

    },

    logMessage: function () { },

    connectAdb: function (success, failed, i) {
        i--;
        if (i < 0) {
            private.logMessage("ADB connect timeout");
            failed();
            return;
        }
        private.logMessage("ADB try connect #", i);
        const child = private.exec('adb disconnect; adb connect localhost:5555&',
            (error, stdout, stderr) => {
                private.logMessage(`stdout: ${stdout}`);
                private.logMessage(`stderr: ${stderr}`);
                if (stdout.indexOf("unable to connect") > -1) {
                    private.logMessage("ADB connect FAILED");
                      setTimeout(function() {
                        private.connectAdb(success, failed, i);
                      }, 10000);
                } else {
                    success();
                }
                /*else if (stdout.indexOf("connected") > -1) {
                    private.logMessage("ADB connected SUCCESSFULLY");
                }*/
                if (error !== null) {
                    private.logMessage(`exec error: ${error}`);
                }
            });
    },

    startInstance: function (resolve, reject, i) {
        i--;
        if (i < 0) {
            private.logMessage("Genymotion Instance start procedure timeout");
            reject(new Error("Start instance timeout"));
            return;
        }
        private.ec2.startInstances(private.paramsInstance, function (err, data) {
            if (err) {
                private.logMessage(err); // an error occurred
                console.log(err.stack);
                setTimeout(function() {
                    private.logMessage("Genymotion Instance start procedure failed and delayed");
                    private.startInstance(resolve, reject, i);
                }, 15000);
            } else {
                private.logMessage("Genymotion Instance returned the response for the 'Start' request."); // successful response
                private.logMessage('Previous state:', data.StartingInstances[0].PreviousState.Name, '. Current state:', data.StartingInstances[0].CurrentState.Name);
                resolve(data.StartingInstances[0].CurrentState.Name);
            }
        });
    },

    stopInstance: function (i, resolve, reject) {
        i--;
        if (i < 0) {
            private.logMessage("Genymotion Instance stop procedure timeout");
            if (reject) reject(new Error("Stop instance timeout"));
            return;
        }

        private.ec2.stopInstances(private.paramsInstance, function (err, data) {
            if (err) {
                private.logMessage(err); // an error occurred
                console.log(err.stack);
                setTimeout(function() {
                    private.logMessage("Genymotion Instance stop procedure failed and delayed");
                    private.stopInstance(i, resolve, reject);
                }, 15000);
            }
            else {
                private.logMessage("Genymotion Instance returned the response for the 'Stop' request."); // successful response
                private.waitStoppedState(resolve, reject);
            }
        });
    },

    waitStoppedState: function (resolve, reject) {
        let isFirstTimeout = false;
        let firstTimeout = null;

        let cancelTimeouts = function () {
            if (firstTimeout) clearTimeout(firstTimeout);
        };

        private.waitStoppedInternal(isFirstTimeout, cancelTimeouts, resolve, reject);

        firstTimeout = setTimeout(function () {
            private.logMessage('Waiting the stopped state stuck after 180 seconds. Terminating.');
            isFirstTimeout = true;
            reject(new Error('Timeout error has occured on checking the stopped Genymotion state'));
        }, 180000);
    },
    waitStoppedInternal: function (timeoutFlag, cancelTimeouts, resolve, reject) {
        private.statusPromise('stopped').then(
            function (data) {
                cancelTimeouts();

                let status = data.Reservations[0].Instances[0].State.Name;
                private.logMessage('Waiting the stopped state. Current instance state:', status);

                if (!timeoutFlag) resolve(status);
            },
            function (error) {
                private.logMessage(error);
                if (!timeoutFlag) reject(error);
            });
    },

    waitExistingState: function (resolve, reject) {
        let isFirstTimeout = false;
        let firstTimeout = null;

        let cancelTimeouts = function () {
            if (firstTimeout) clearTimeout(firstTimeout);
        };

        private.waitExistingInternal(isFirstTimeout, cancelTimeouts, resolve, reject);

        firstTimeout = setTimeout(function () {
            private.logMessage('Checking the exists state stuck after 30 seconds. Terminating.');
            isFirstTimeout = true;
            reject(new Error('Timeout error has occured on checking the exists Genymotion state'));
        }, 30000);
    },
    waitExistingInternal: function (timeoutFlag, cancelTimeouts, resolve, reject) {
        private.statusPromise('exists').then(
            function (data) {
                cancelTimeouts();

                try {
                    let status = data.Reservations[0].Instances[0].State.Name;
                    private.logMessage('Checking the exists state. Current instance state:', status);

                    if (!timeoutFlag) resolve(status);
                } catch (e) {
                    private.logMessage('An error has occured on checking the exists Genymotion state.', e);
                    if (!timeoutFlag) reject(e);
                }
            },
            function (error) {
                private.logMessage(error);
                if (!timeoutFlag) reject(error);
            });
    },

    start: function (resolve, reject) {
        let startInstanceConditional = function (instanceState) {

            switch (instanceState) {
                case 'running':
                    private.logMessage("Instance is already in running state. No need to wait it.");
                    private.checkAdb(false, resolve, reject);
                    break;

                case 'stopping':
                    private.waitStoppedState(startInstanceConditional, startInstanceConditional);
                    break;
                default:
                    private.startInstance(function (instanceState) {
                        private.logMessage("Start instance procedure successfully executed");
                        if (instanceState == 'running') {
                            private.logMessage("Instance is already in running state. No need to wait it.");
                            private.checkAdb(false, resolve, reject);
                        } else {
                            private.waitRunningState(resolve, reject);
                        }
                    }, function (error) {
                        private.logMessage("Start instance procedure executed with an error");
                        reject(error);
                    }, 5);
                    break;
            }

        };

        private.waitExistingState(startInstanceConditional, startInstanceConditional);
    },

    waitRunningState: function (resolve, reject) {
        let isFirstTimeout = false;
        let firstTimeout = null;
        let isSecondTimeout = false;
        let secondTimeout = null;

        let cancelTimeouts = function () {
            if (firstTimeout) clearTimeout(firstTimeout);
            if (secondTimeout) clearTimeout(secondTimeout);
        };

        private.waitRunningInternal(isFirstTimeout, cancelTimeouts, resolve, reject);

        firstTimeout = setTimeout(function () {
            private.logMessage('Waiting the running state stuck after 30 seconds. Restarting.');
            isFirstTimeout = true;
            private.waitRunningInternal(isSecondTimeout, cancelTimeouts, resolve, reject);
        }, 30000);

        secondTimeout = setTimeout(function () {
            private.logMessage('Waiting the running state stuck after 60 seconds. Terminating.');
            isSecondTimeout = true;
            reject(new Error('Timeout error has occured on checking the running Genymotion state'));
        }, 60000);
    },
    waitRunningInternal: function (timeoutFlag, cancelTimeouts, resolve, reject) {
        private.statusPromise('running').then(
            function (data) {
                cancelTimeouts();

                let status = data.Reservations[0].Instances[0].State.Name;
                private.logMessage('Waiting the running state. Current instance state:', status);

                private.checkAdb(timeoutFlag, resolve, reject);
            },
            function (error) {
                private.logMessage(error);
                if (!timeoutFlag) reject(error);
            });
    },
    checkAdb: function (timeoutFlag, resolve, reject) {
        setTimeout(function () {
            private.connectAdb(function () {
                if (!timeoutFlag) resolve('running'); //pass constant 'running' here because it is current instance state
            }, function () { // failed restart check instance started
                private.logMessage("Connect ADB failed for 5 attempts");
                if (!timeoutFlag) reject(new Error('Failed to connect ADB'));
            }, 5);
        }, 1000);
    },

    stop: function (resolve, reject) {
        let stopInstanceConditional = function (instanceState) {

            switch (instanceState) {
                case 'stopped':
                    private.logMessage("Instance is already in stopped state. No need to wait it.");
                    resolve(instanceState);
                    break;
                case 'stopping':
                    private.waitStoppedState(stopInstanceConditional, stopInstanceConditional);
                    break;
                default:
                    private.stopInstance(3, resolve, reject);
                    break;
            }

        };

        private.waitExistingState(stopInstanceConditional, stopInstanceConditional);
    },

    startPromise: function () {
        private.logMessage("Genymotion Instance start procedure begin");
        let promise = new Promise(function (resolve, reject) {
            private.start(resolve, reject);
        });

        return promise;
    },
    stopPromise: function () {
        private.logMessage("Genymotion Instance stop procedure begin");
        let promise = new Promise(function (resolve, reject) {
            private.stop(resolve, reject);
        });

        return promise;
    },
    statusPromise: function (statusType) {
        let promise = new Promise(function (resolve, reject) {
            let waiter = null;
            switch (statusType) {
                case 'system':
                    waiter = 'systemStatusOk'
                    break;
                case 'exists':
                    waiter = 'instanceExists';
                    break;
                case 'running':
                    waiter = 'instanceRunning';
                    break;
                case 'stopped':
                    waiter = 'instanceStopped';
                    break;
                case 'instance':
                default:
                    waiter = 'instanceStatusOk';
                    break;
            };

            private.ec2.waitFor(waiter, private.paramsInstance, function (err, data) {
                if (err) {
                    private.logMessage(err);
                    console.log(err.stack); // an error occurred

                    reject(new Error('Error has occured on "' + waiter + '" call'));
                } else {
                    resolve(data);
                }
            });
        });

        return promise;
    },

    waitTimeout: function (timeout) {
        let promise = new Promise(function (resolve, reject) {
            setTimeout(resolve, timeout);
        });

        return promise;
    },

    shutdownAppiumPromise: function (port) {
        port = port || 4725;

        let promise = new Promise(function (resolve, reject) {
            private.exec("lsof -P | grep ':" + port + "' | awk '{print $2}' | xargs kill -9", (error, stdout, stderr) => {
                if (error !== null) {
                    private.logMessage(`exec error: ${error}`);
                    return reject(error);
                }

                setTimeout(function () {
                    return resolve();
                }, 5000);
            });
        });

        return promise;
    },
};

module.exports = function (logFunction) {
    private.init(logFunction);

    return {
        startPromise: private.startPromise,
        stopPromise: private.stopPromise,
        waitTime: private.waitTimeout,
        statusPromise: private.statusPromise,
        shutdownAppiumPromise: private.shutdownAppiumPromise
    };
};
