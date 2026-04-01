class ExpensesController < ApplicationController
  before_action :set_expense, only: %i[ show edit update destroy ]

  # GET /expenses or /expenses.json
  def index
    # 1. Get today's expenses (Keep your current logic)
    @expenses = Expense.where(entry_date: Date.today).order(created_at: :desc)
  
    # 2. Calculate totals
    @total_amount = @expenses.sum(:amount)
    
    # 3. Audit Logic
    @starting_cash = 20000.00 
    @expected_cash = @starting_cash - @total_amount
  end

  # GET /expenses/1 or /expenses/1.json
  def show
  end

  # GET /expenses/new
  def new
    @expense = Expense.new
  end

  def report
  # Capture dates from the form, or default to the beginning of the month
  @start_date = params[:start_date].presence || Date.today.beginning_of_month.to_s
  @end_date = params[:end_date].presence || Date.today.to_s

  @expenses = Expense.where(entry_date: @start_date..@end_date).order(entry_date: :desc)
  @total_amount = @expenses.sum(:amount)
  end
  # GET /expenses/1/edit
  def edit
  end

  # POST /expenses or /expenses.json
  def create
    # current_user.expenses.build automatically sets the user_id for the new expense
    @expense = current_user.expenses.build(expense_params)

    # TRIGGER THE AI SCAN HERE
    # This runs the logic in your model to fill in amount/description from the photo
    if @expense.receipt_photo.attached?
      @expense.analyze_receipt 
    end

    respond_to do |format|
      if @expense.save
        # Redirecting to root_path (the dashboard) so your auntie sees the updated table immediately
        format.html { redirect_to root_path, notice: "Expense was successfully recorded by #{current_user.name}." }
        format.json { render :show, status: :created, location: @expense }
      else
        format.html { render :new, status: :unprocessable_entity }
        format.json { render json: @expense.errors, status: :unprocessable_entity }
      end
    end
  end

  # PATCH/PUT /expenses/1 or /expenses/1.json
  def update
    respond_to do |format|
      if @expense.update(expense_params)
        format.html { redirect_to @expense, notice: "Expense was successfully updated.", status: :see_other }
        format.json { render :show, status: :ok, location: @expense }
      else
        format.html { render :edit, status: :unprocessable_entity }
        format.json { render json: @expense.errors, status: :unprocessable_entity }
      end
    end
  end

  # DELETE /expenses/1 or /expenses/1.json
  def destroy
    @expense.destroy!

    respond_to do |format|
      format.html { redirect_to expenses_path, notice: "Expense was successfully destroyed.", status: :see_other }
      format.json { head :no_content }
    end
  end

  private
    # Use callbacks to share common setup or constraints between actions.
    def set_expense
      @expense = Expense.find(params.expect(:id))
    end

    # Only allow a list of trusted parameters through.
    def expense_params
      params.expect(expense: [ :entry_date, :description, :amount, :department, :pcf_source, :is_verified, :notes, :receipt_photo ])
    end
end
