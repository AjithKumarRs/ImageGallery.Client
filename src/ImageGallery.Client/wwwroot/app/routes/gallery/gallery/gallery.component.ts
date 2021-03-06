import { Component, OnInit, ViewContainerRef, ChangeDetectorRef } from '@angular/core';
import { GalleryService } from '../../../gallery.service';
import { IGalleryIndexViewModel } from '../../../shared/interfaces';
import { AuthService } from '../../../services/auth.service';
import { ToastrService } from 'ngx-toastr';
import { NgxLoadingSpinnerService } from 'ngx-loading-spinner-fork';
import { setTimeout } from 'timers';


@Component({
    selector: 'app-gallery',
    templateUrl: './gallery.component.html',
    styleUrls: ['./gallery.component.scss'],
    providers: [GalleryService]
})
export class GalleryComponent implements OnInit {

    galleryIndexViewModel: IGalleryIndexViewModel;
    pagination = {
        page: 1,
        limit: 15,
        totalItems: 10
    };
    perPage = [15, 30, 60, 90];

    constructor(
        private authService: AuthService,
        private galleryService: GalleryService,
        public toastr: ToastrService, 
        vcr: ViewContainerRef,
        private spinnerService: NgxLoadingSpinnerService,
        private changeDetectorRef: ChangeDetectorRef
    ) {
        //this.toastr.setRootViewContainerRef(vcr);
    }

    ngOnInit() {
        this.authService.getIsAuthorized().subscribe(
            (isAuthorized: boolean) => {
                if (isAuthorized) {
                    this.pagination.limit = localStorage.getItem('limit') ? parseInt(localStorage.getItem('limit')) : 15;
                    this.pagination.page = localStorage.getItem('page') ? parseInt(localStorage.getItem('page')) : 1;
                    this.getGalleryIndexViewModel();            
                }
            });
    }

    ngAfterViewInit() {
        if (localStorage.getItem('isEdited') == 'yes') {
            this.toastr.toastrConfig.closeButton = true;
            this.toastr.success('Image has been edited successfully!', 'Success!', { closeButton: true });
            localStorage.removeItem('isEdited');
        } else if (localStorage.getItem('isAdded') == 'yes') {
            this.toastr.success('Image has been added successfully!', 'Success!', { closeButton: true});
            localStorage.removeItem('isAdded');
        }
        setTimeout(() => {
            this.pagination.page = localStorage.getItem('page') ? parseInt(localStorage.getItem('page')) : 1;
            this.changeDetectorRef.detectChanges();
        }, 1000);
    }

    public deleteImage(imageId: string) {
        this.galleryService.deleteImageViewModel(imageId)
            .subscribe((response) => { },
            (err: any) => {
                this.toastr.error('Failed to delete image!', 'Oops!', { closeButton: true });
                console.log(err);
            },
            () => {
                this.toastr.success('Image has been deleted successfully!', 'Success!', { closeButton: true});
                this.galleryIndexViewModel.images = this.galleryIndexViewModel.images.filter(x => x.id != imageId);
            });
    }

    private getGalleryIndexViewModel(event?) {
        this.spinnerService.show();

        if (typeof event == 'string') {
            this.pagination.limit = parseInt(event);
            this.pagination.page = 1;
            localStorage.setItem('limit', this.pagination.limit.toString());
            localStorage.setItem('page', this.pagination.page.toString());
        } else if (typeof event == 'object') {
            this.pagination.limit = event.itemsPerPage;
            this.pagination.page = event.page;
            localStorage.setItem('limit', this.pagination.limit.toString());
            localStorage.setItem('page', this.pagination.page.toString());
        }
        setTimeout(() => {
            this.pagination.page = localStorage.getItem('page') ? parseInt(localStorage.getItem('page')) : 1;
            this.changeDetectorRef.detectChanges();
        }, 1000);

        this.galleryService.getGalleryIndexViewModel(this.pagination.limit, this.pagination.page)
        .then((response: any) => {
            this.galleryIndexViewModel = response.images;
            this.pagination.totalItems = response.totalCount;
            this.scrollToTop();
            this.spinnerService.hide();
        }).catch(err => {
            this.toastr.error('Access is denied!', 'Oops!', { closeButton: true});
            this.spinnerService.hide();
        });
    }

    private scrollToTop() {
        window.scrollTo(0, 0);
    }
}
