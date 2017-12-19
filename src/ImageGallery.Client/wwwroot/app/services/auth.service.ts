import { Injectable, Component, OnInit, OnDestroy, Inject } from '@angular/core';
import { Observable } from 'rxjs/Rx';
import { Subscription } from 'rxjs/Subscription';

import { OidcSecurityService, OpenIDImplicitFlowConfiguration } from 'angular-auth-oidc-client';

@Injectable()
export class AuthService implements OnInit, OnDestroy {
    isAuthorizedSubscription: Subscription;
    isAuthorized: boolean;

    constructor(private oidcSecurityService: OidcSecurityService,
        @Inject('ORIGIN_URL') originUrl: string,
        @Inject('IDENTITY_URL') identityUrl: string
    ) {
        console.log(`Ctor of [AuthService]`)

        const openIdImplicitFlowConfiguration = new OpenIDImplicitFlowConfiguration();
        openIdImplicitFlowConfiguration.stsServer = identityUrl;
        openIdImplicitFlowConfiguration.redirect_url = originUrl + 'callback';

        console.log(`redirect_url -> ${openIdImplicitFlowConfiguration.redirect_url}`)

        openIdImplicitFlowConfiguration.client_id = 'imagegalleryjsclient';
        openIdImplicitFlowConfiguration.response_type = 'id_token token';
        openIdImplicitFlowConfiguration.scope = 'roles openid profile address country imagegalleryapi subscriptionlevel';
        openIdImplicitFlowConfiguration.post_logout_redirect_uri = originUrl;

        console.log(`post_logout_redirect_uri -> ${openIdImplicitFlowConfiguration.post_logout_redirect_uri}`);

        openIdImplicitFlowConfiguration.forbidden_route = '/forbidden';
        openIdImplicitFlowConfiguration.unauthorized_route = '/unauthorized';
        openIdImplicitFlowConfiguration.auto_userinfo = true;
        openIdImplicitFlowConfiguration.log_console_warning_active = true;
        openIdImplicitFlowConfiguration.log_console_debug_active = false;
        openIdImplicitFlowConfiguration.max_id_token_iat_offset_allowed_in_seconds = 10;
        openIdImplicitFlowConfiguration.log_console_debug_active = true;
        openIdImplicitFlowConfiguration.log_console_warning_active = true;

        this.oidcSecurityService.setupModule(openIdImplicitFlowConfiguration);

        if (this.oidcSecurityService.moduleSetup) {
            console.log(`Property [moduleSetup] of [OidcSecurityService] configured properly`);

            this.doCallbackLogicIfRequired();
        } else {
            console.log(`Property [moduleSetup] of [OidcSecurityService] first time to load well know endpoints`);

            this.oidcSecurityService.onModuleSetup.subscribe(() => {
                console.log(`[onModuleSetup] raise finished call doCallbackLogicIfRequired`);

                this.doCallbackLogicIfRequired();
            });
        }
    }

    ngOnInit() {
        console.log(`[ngOnInit] AuthService`);

        this.isAuthorizedSubscription = this.oidcSecurityService.getIsAuthorized().subscribe(
            (isAuthorized: boolean) => {
                this.isAuthorized = isAuthorized;
            });
    }

    ngOnDestroy(): void {
        console.log(`[ngOnDestroy] AuthService`);

        this.isAuthorizedSubscription.unsubscribe();
        this.oidcSecurityService.onModuleSetup.unsubscribe();
    }

    getIsAuthorized(): Observable<boolean> {
        return this.oidcSecurityService.getIsAuthorized();
    }

    login() {
        console.log('[login] of AuthService');
        this.oidcSecurityService.authorize();
    }

    refreshSession() {
        console.log('[refreshSession] AuthService');
        this.oidcSecurityService.authorize();
    }

    logout() {
        console.log('[logout] AuthService');
        this.oidcSecurityService.logoff();
    }

    private doCallbackLogicIfRequired() {
        let hash = window.location.hash;
        console.log(`location is ${location} and window.location.hash is ${hash}`)

        if (typeof location !== "undefined" && hash) {
            console.log(`[OidcSecurityService] -> [authorizedCallback()]`);

            this.oidcSecurityService.authorizedCallback();
        } else {
            console.log(`[OidcSecurityService] -> [getToken()]`);

            let token = this.oidcSecurityService.getToken();
            if (!token) {
                console.log(`[AuthService] -> [login] call`);

                this.login()
            }
        }
    }
}