import { Component, Inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormArray } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TICKET_SERVICE_TOKEN } from '../../Core/injection-tokens';
import { TicketService, ProductDatas } from '../../Core/services/ticket.service';

@Component({
  selector: 'app-product-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './product-form.html',
  styleUrls: ['./product-form.scss']
})
export class ProductFormComponent implements OnInit {
  public productForm: FormGroup;
  public isEditMode = false;
  public isLoading = false;
  public pageTitle = 'Create New Product';
  private productId: number = 0;
  private returnPmId: number = 0; // Property to store the PM's ID for navigation

  constructor(
    private fb: FormBuilder,
    @Inject(TICKET_SERVICE_TOKEN) private ticketService: TicketService,
    private route: ActivatedRoute,
    private router: Router
  ) {
    this.productForm = this.fb.group({
      id: [0],
      name: ['', Validators.required],
      modules: this.fb.array([])
    });
  }

  ngOnInit(): void {
    // Read the 'pmId' from the URL's query parameters
    this.route.queryParamMap.subscribe(params => {
      this.returnPmId = Number(params.get('pmId'));
    });

    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.isEditMode = true;
      this.productId = Number(id);
      this.pageTitle = 'Edit Product';
      this.loadProductData();
    }
  }

  async loadProductData(): Promise<void> {
    this.isLoading = true;
    try {
      const allProducts = await this.ticketService.getProductHierarchys();
      const productToEdit = allProducts.find(p => Number(p.id) === this.productId);
      if (productToEdit) {
        this.productForm.patchValue({
          id: productToEdit.id,
          name: productToEdit.name
        });
        productToEdit.modules.forEach(module => {
          this.addModule(module);
        });
      }
    } finally {
      this.isLoading = false;
    }
  }

  modules(): FormArray {
    return this.productForm.get('modules') as FormArray;
  }
  subModules(moduleIndex: number): FormArray {
    return this.modules().at(moduleIndex).get('subModules') as FormArray;
  }

  addModule(moduleData?: any): void {
    const moduleGroup = this.fb.group({
      id: [moduleData?.id || 0],
      name: [moduleData?.name || '', Validators.required],
      subModules: this.fb.array([])
    });
    this.modules().push(moduleGroup);

    if (moduleData?.subModules) {
      const subModulesArray = moduleGroup.get('subModules') as FormArray;
      moduleData.subModules.forEach((subModule: any) => {
        subModulesArray.push(this.fb.group({
          id: [subModule.id || 0],
          name: [subModule.name || '', Validators.required]
        }));
      });
    }
  }

  removeModule(index: number): void {
    this.modules().removeAt(index);
  }

  addSubModule(moduleIndex: number): void {
    this.subModules(moduleIndex).push(this.fb.group({
      id: [0],
      name: ['', Validators.required]
    }));
  }

  removeSubModule(moduleIndex: number, subModuleIndex: number): void {
    this.subModules(moduleIndex).removeAt(subModuleIndex);
  }

  async save(): Promise<void> {
    if (this.productForm.invalid) return;

    if (this.isEditMode) {
      await this.ticketService.updateProduct(this.productId, this.productForm.value);
    } else {
      await this.ticketService.createProduct(this.productForm.value);
    }
    // After saving, navigate back to the product list with the pmId
    this.router.navigate(['/masterform/products'], {
      queryParams: { pmId: this.returnPmId }
    });
  }

  cancel(): void {
    // Also navigate back to the product list with the pmId
    this.router.navigate(['/masterform/products'], {
      queryParams: { pmId: this.returnPmId }
    });
  }
}