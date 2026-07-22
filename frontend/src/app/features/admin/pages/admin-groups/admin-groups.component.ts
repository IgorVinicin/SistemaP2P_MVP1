import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormsModule, FormBuilder, FormGroup, Validators, ValidatorFn, AbstractControl, ValidationErrors } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatListModule } from '@angular/material/list';
import { MatCardModule } from '@angular/material/card';
import { AdminGroupService, InvestorGroup, CreateUpdateGroupDto } from '../../../../core/services/admin-group.service';
import { AdminService } from '../../../../core/services/admin.service';
import { AdminCreditService } from '../../../../core/services/admin-credit.service';

function maxSelectedValidator(max: () => number): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const limit = max();
    if (control.value && Array.isArray(control.value) && control.value.length > limit) {
      return { maxSelected: true };
    }
    return null;
  };
}

@Component({
  selector: 'app-admin-groups',
  standalone: true,
  imports: [
    CommonModule, ReactiveFormsModule, FormsModule, MatTableModule, MatButtonModule, 
    MatIconModule, MatDialogModule, MatSnackBarModule, MatInputModule, MatFormFieldModule,
    MatSelectModule, MatListModule, MatCardModule
  ],
  templateUrl: './admin-groups.component.html',
  styleUrls: ['./admin-groups.component.css']
})
export class AdminGroupsComponent implements OnInit {
  private groupService = inject(AdminGroupService);
  private adminService = inject(AdminService);
  private creditService = inject(AdminCreditService);
  private snackBar = inject(MatSnackBar);
  public dialog = inject(MatDialog);
  private fb = inject(FormBuilder);

  public maxInvestorsLimit = 10;
  public groups: InvestorGroup[] = [];
  public displayedColumns: string[] = ['nome', 'descricao', 'investorsCount', 'createdAt', 'actions'];
  
  public investorsList: any[] = [];
  public investorSearch: string = '';
  public groupForm!: FormGroup;
  public editingGroupId: string | null = null;
  public isModalOpen = false;

  get filteredInvestors(): any[] {
    if (!this.investorSearch) return this.investorsList;
    const search = this.investorSearch.toLowerCase();
    return this.investorsList.filter(inv => 
      (inv.nome && inv.nome.toLowerCase().includes(search)) ||
      (inv.email && inv.email.toLowerCase().includes(search)) ||
      (inv.cpf && inv.cpf.includes(search))
    );
  }

  ngOnInit() {
    this.loadConfig();
    this.loadGroups();
    this.loadInvestors();
    this.initForm();
  }

  loadConfig() {
    this.creditService.getConfig().subscribe({
      next: (config: any) => {
        if (config && config.maxInvestidoresPorEmprestimo) {
          this.maxInvestorsLimit = config.maxInvestidoresPorEmprestimo;
        }
      }
    });
  }

  initForm() {
    this.groupForm = this.fb.group({
      nome: ['', Validators.required],
      descricao: [''],
      investorIds: [[], maxSelectedValidator(() => this.maxInvestorsLimit)]
    });
  }

  loadGroups() {
    this.groupService.getGroups().subscribe({
      next: (data: any) => this.groups = data,
      error: (err: any) => this.snackBar.open('Erro ao carregar grupos.', 'Fechar', { duration: 3000 })
    });
  }

  loadInvestors() {
    this.adminService.getAllUsers(1, 1000, '', { role: 'Investidor', status: 'Aprovado' }).subscribe({
      next: (res: any) => {
        this.investorsList = res.items || [];
      },
      error: (err: any) => this.snackBar.open('Erro ao carregar investidores.', 'Fechar', { duration: 3000 })
    });
  }

  openModal(group?: InvestorGroup) {
    if (group) {
      this.editingGroupId = group.id;
      
      this.groupService.getGroup(group.id).subscribe({
        next: (g: any) => {
          this.groupForm.patchValue({
            nome: g.nome,
            descricao: g.descricao,
            investorIds: g.investors.map((i: any) => i.investorId)
          });
          this.isModalOpen = true;
        }
      });
    } else {
      this.editingGroupId = null;
      this.groupForm.reset({ investorIds: [] });
      this.investorSearch = '';
      this.isModalOpen = true;
    }
  }

  closeModal() {
    this.isModalOpen = false;
  }

  saveGroup() {
    if (this.groupForm.invalid) return;

    const formValue = this.groupForm.value;
    const dto: CreateUpdateGroupDto = {
      nome: formValue.nome,
      descricao: formValue.descricao,
      investorIds: formValue.investorIds || []
    };
    
    if (this.editingGroupId) {
      this.groupService.updateGroup(this.editingGroupId, dto).subscribe({
        next: () => {
          this.snackBar.open('Grupo atualizado com sucesso.', 'Fechar', { duration: 3000 });
          this.closeModal();
          this.loadGroups();
        },
        error: (err: any) => {
          let msg = err.error?.message;
          if (!msg && err.error?.errors) msg = JSON.stringify(err.error.errors);
          if (!msg) msg = `Erro ${err.status}: ${err.message}`;
          this.snackBar.open(msg, 'Fechar', { duration: 5000 });
        }
      });
    } else {
      this.groupService.createGroup(dto).subscribe({
        next: () => {
          this.snackBar.open('Grupo criado com sucesso.', 'Fechar', { duration: 3000 });
          this.closeModal();
          this.loadGroups();
        },
        error: (err: any) => {
          let msg = err.error?.message;
          if (!msg && err.error?.errors) msg = JSON.stringify(err.error.errors);
          if (!msg) msg = `Erro ${err.status}: ${err.message}`;
          this.snackBar.open(msg, 'Fechar', { duration: 5000 });
        }
      });
    }
  }

  deleteGroup(id: string) {
    if (confirm('Tem certeza que deseja excluir este grupo?')) {
      this.groupService.deleteGroup(id).subscribe({
        next: () => {
          this.snackBar.open('Grupo excluído com sucesso.', 'Fechar', { duration: 3000 });
          this.loadGroups();
        },
        error: (err: any) => this.snackBar.open('Erro ao excluir grupo', 'Fechar', { duration: 3000 })
      });
    }
  }

  getSelectedInvestorsFull(): any[] {
    const selectedIds = this.groupForm.get('investorIds')?.value || [];
    return this.investorsList.filter(inv => selectedIds.includes(inv.id));
  }

  removeInvestor(investorId: string) {
    const currentIds = this.groupForm.get('investorIds')?.value || [];
    const updatedIds = currentIds.filter((id: string) => id !== investorId);
    this.groupForm.patchValue({ investorIds: updatedIds });
    this.groupForm.get('investorIds')?.updateValueAndValidity();
  }

  isLimitReached(): boolean {
    const currentIds = this.groupForm.get('investorIds')?.value || [];
    return currentIds.length >= this.maxInvestorsLimit;
  }

  isSelected(investorId: string): boolean {
    const currentIds = this.groupForm.get('investorIds')?.value || [];
    return currentIds.includes(investorId);
  }
}
