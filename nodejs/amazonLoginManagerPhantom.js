const _jsdom = require('jsdom');
const { JSDOM } = _jsdom;

const private = {
    phantom: require('node-phantom-2'),
    readline: require('readline'),

    rl: null,
    proxiesManager: null,

    isUseProxiesOnLogin: false,
    isInteractiveMode: false,

    init: function (proxiesManager, isUseProxiesOnLogin, isInteractiveMode) {
        this.proxiesManager = proxiesManager;
        this.rl = this.readline.createInterface({
            input: process.stdin,
            output: process.stdout
        });

        this.isUseProxiesOnLogin = isUseProxiesOnLogin;
        this.isInteractiveMode = isInteractiveMode;
    },

    signIn: function (login, password) {
        let promise = new Promise(function (resolve, reject) {
            let context = {
                login: login,
                password: password,
                isCompleted: false,
                stuckTimeout: null,
                stuckCount: 0,

                exitPhantom: function () {
                    try { this.ph.exit(); } catch (e) { }
                }
            };

            return Promise.resolve(context).then(function loop(localContext) {
                if (!localContext.isCompleted) {
                    return private.signInPromise(localContext).then(loop);
                } else {
                    return resolve(context.cookie);
                }
            }).catch(function (result) {
                return reject(result);
            });
        });

        return promise;
    },

    signInPromise: function (context) {
        let promise = new Promise(function (resolve, reject) {

            private.phantom.cookiesEnabled = true;
            private.phantom.javascriptEnabled = true;

            let parameters = {
                'cookies-file': 'cookies.txt',
                'load-images': false,
            };

            if (private.isUseProxiesOnLogin && private.proxiesManager.isProxyRequested()) parameters['proxy'] = private.proxiesManager.getProxy();

            private.phantom.create(function (err, ph) {
                if (err) {
                    return reject(new Error('Unable to login. PhantomJS is unavailable.'));
                }

                context.ph = ph;
                private.resetStuckTimeout(context, resolve, reject);

                ph.clearCookies();
                return ph.createPage(function (err, page) {
                    if (err) {
                        context.exitPhantom();
                        return reject(new Error('Unable to login. PhantomJS cannot create a page.'));
                    }

                    page.set('settings', {
                        userAgent: "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/66.0.3359.181 Safari/537.36",
                        javascriptEnabled: true,
                        cookiesEnabled: true,
                        loadImages: false
                    });

                    page.onUrlChanged = function (targetUrl) {
                        console.log('New URL: ' + targetUrl);
                    };
                    page.onLoadStarted = function () {
                        console.log('Page loading started');
                    };
                    page.onNavigationRequested = function (url, type, willNavigate, main) {
                        console.log('Trying to navigate to: ' + url);
                    };

                    return page.open("https://www.amazon.com/", function (err, status) {
                        if (err) {
                            context.exitPhantom();
                            return reject(new Error('Unable to login. PhantomJS cannot open Amazon home page.'));
                        }

                        page.onLoadFinished = function () {
                            private.resetStuckTimeout(context, resolve, reject);
                            console.log('Amazon home page loading finished.');

                            return page.evaluate(function () {
                                document.querySelector('#nav-flyout-ya-signin a').click();
                            }, function (err, result) {
                                if (err) {
                                    context.exitPhantom();
                                    return reject(new Error('Unable to login. PhantomJS cannot open Amazon sign-in page.'));
                                }

                                page.onLoadFinished = function () {
                                    private.resetStuckTimeout(context, resolve, reject);
                                    console.log('Amazon first sign-in page loading finished.');

                                    return page.evaluate(function (credentials) { //evaluating the first setp of authentication
                                        document.getElementById("ap_email").value = credentials.login;
                                        document.querySelector('form[name="signIn"]').submit();

                                    }, function (err, result) {
                                        if (err) {
                                            context.exitPhantom();
                                            return reject(new Error('Unable to login. PhantomJS cannot pass the first Amazon authentication step.'));
                                        }

                                        page.onLoadFinished = function () {
                                            private.resetStuckTimeout(context, resolve, reject);
                                            console.log('Amazon second sign-in page loading finished.');

                                            return page.evaluate(function (credentials) { //evaluating the second step of authentication
                                                document.getElementById("ap_password").value = credentials.password;
                                                document.querySelector('form[name="signIn"]').submit();

                                            }, function (err, result) {
                                                if (err) {
                                                    context.exitPhantom();
                                                    return reject(new Error('Unable to login. PhantomJS cannot pass the second Amazon authentication step.'));
                                                }

                                                page.onLoadFinished = function () {
                                                    if (context.stuckTimeout) clearTimeout(context.stuckTimeout);
                                                    console.log('Amazon home page after the login loading finished.');

                                                    return page.evaluate(function () {
                                                        return {
                                                            cookie: document.cookie,
                                                            id: document.getElementById("twotabsearchtextbox") ? document.getElementById("twotabsearchtextbox").id : null,
                                                            claimspicker: document.querySelector('form[name="claimspicker"] input[name="clientContext"]') ?
                                                                          document.querySelector('form[name="claimspicker"] input[name="clientContext"]').value : null
                                                        };
                                                    }, function (err, result) {
                                                        if (err) {
                                                            context.exitPhantom();
                                                            return reject(new Error('Unable to login. PhantomJS cannot get the cookies from Amazon.'));
                                                        }

                                                        page.onLoadFinished = function () { };
                                                        page.onLoadStarted = function () { };

                                                        if (result.id) {
                                                            context.cookie = result.cookie;
                                                            context.isCompleted = true;

                                                            context.exitPhantom();
                                                            return resolve(context);
                                                        } else {
                                                            if (result.claimspicker) { //verification page faced
                                                                if (private.isInteractiveMode) { //in case of interactive mode scrapper will ask the verification from console
                                                                    private.passVerificationDuringSingIn(page, context, resolve, reject);
                                                                } else return reject(new Error('Unable to login. Target page is Amazon Verification Request page.'));
                                                            }
                                                            else {
                                                                context.exitPhantom();
                                                                return reject(new Error('Unable to login. Target page is not Amazon home page.'));
                                                            }
                                                        }
                                                    });
                                                }
                                            },
                                            {
                                                password: context.password
                                            });

                                        }
                                    },
                                    {
                                        login: context.login
                                    });
                                };

                            });
                        };
                    });
                });

            }
            ,{
                parameters: parameters
            }
            );
        });

        return promise;
    },

    passVerificationDuringSingIn: function (page, context, resolve, reject) {
        return page.evaluate(function () { //evaluating the verification page with button
            document.querySelector('form[name="claimspicker"]').submit();

        }, function (err, result) {
            if (err) {
                context.exitPhantom();
                return reject(new Error('Unable to login. PhantomJS cannot pass Amazon Verification Page.'));
            }

            page.onLoadFinished = function () {
                //read the verification code
                private.rl.question('Please enter the Amazon verification code: ', (code) => {
                    console.log('Amazon verification code is entered');
                    private.rl.close();

                    return page.evaluate(function (code) { //evaluating the verification page with form
                        document.querySelector('form[action="verify"] input').value = code;
                        document.querySelector('form[action="verify"]').submit();

                        return code;
                    }, function (err, result) {
                        if (err) {
                            context.exitPhantom();
                            return reject(new Error('Unable to login. PhantomJS cannot pass Amazon Verification Page form.'));
                        }

                        page.onLoadFinished = function () {

                            return page.evaluate(function () {
                                return {
                                    cookie: document.cookie,
                                    id: document.getElementById("twotabsearchtextbox") ? document.getElementById("twotabsearchtextbox").id : null,
                                };
                            }, function (err, result) {
                                if (err) {
                                    context.exitPhantom();
                                    return reject(new Error('Unable to login. PhantomJS cannot get the cookies from Amazon.'));
                                }

                                page.onLoadFinished = function () { };
                                page.onLoadStarted = function () { };

                                if (result.id) {
                                    context.cookie = result.cookie;
                                    context.isCompleted = true;

                                    context.exitPhantom();
                                    return resolve(context);
                                } else {
                                    context.exitPhantom();
                                    return reject(new Error('Unable to login. Target page is not Amazon home page.'));
                                }
                            });

                        }
                    }, code);
                });
            }
        });
    },

    resetStuckTimeout: function (context, resolve, reject) {
        if (context.stuckTimeout) clearTimeout(context.stuckTimeout);
        context.stuckTimeout = setTimeout(function () {
            context.exitPhantom();
            return private.serveError(context, resolve, reject);
        }, 30000);
    },

    serveError: function (context, resolve, reject) {
        console.log('Login request stuck');

        ++context.stuckCount;
        if (context.stuckCount > 5) {
            context.exitPhantom();
            return reject(new Error('Unable to login. Impossible to perform load pages using PhantomJS'));
        } else {
            return resolve(context);
        }
    }
}

module.exports = function (proxiesManager, isUseProxiesOnLogin, isInteractiveMode) {
    private.init(proxiesManager, isUseProxiesOnLogin, isInteractiveMode);

    return {
        signIn: private.signIn,
    };
};